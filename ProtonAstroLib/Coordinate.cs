using System;

namespace ProtonAstroLib
{

    public struct WGS84Coordinate(Angle lat, Angle lon)
    { 
        public readonly Angle Latitude { get; init; } = lat;
        public readonly Angle Longitude { get; init; } = lon;
    }

    public struct HorizontalCoordinate
    {
        public readonly Angle Altitude { get; init; }
        public readonly Angle Azimuth { get; init; }

        public HorizontalCoordinate(Angle alt, Angle az)
        { Altitude = alt.Normalized; Azimuth = az.Normalized; }

        public Angle Distance(HorizontalCoordinate other)
        {
            var altdiff = (double)(Altitude - other.Altitude);
            var azdiff = (double)(Azimuth - other.Azimuth);
            return (Angle)Math.Sqrt(altdiff * altdiff + azdiff * azdiff);
        }
    }

    public struct EquatorialCoordinate(Angle ra, Angle dec, DateTimeOffset? epoch = null)
    {
        /// <summary>
        /// The reference epoch for these coordinates. J2000 for catalog stars,
        /// or the observation time for coordinates already in equinox-of-date (e.g. the Sun).
        /// </summary>
        public readonly DateTimeOffset Epoch { get; } = epoch ?? Constants.J2000Noon_UT;

        public static EquatorialCoordinate FromRaDec(string ra, string dec)
        {
            return new EquatorialCoordinate(Angle.FromHMS(ra), Angle.FromDegrees(dec));
        }

        public readonly Angle RightAscension { get { return ra; } }
        public readonly Angle Declination { get { return dec; } }

        public Angle Distance(EquatorialCoordinate other)
        {
            var altdiff = (double)(RightAscension - other.RightAscension);
            var azdiff = (double)(Declination - other.Declination);
            return (Angle)Math.Sqrt(altdiff * altdiff + azdiff * azdiff);
        }


        // syntactic sugar
        static readonly Func<Angle, double> sin = a => Math.Sin((double)a);
        static readonly Func<Angle, double> cos = a => Math.Cos((double)a);

        // All wikipedia below is accessed 7 may 2012

        /// <summary>
        /// Applies IAU precession (Lieske 1979) to precess from this coordinate's Epoch
        /// to the equinox of the given date. Returns a no-op if the Epoch already matches.
        /// Only supports precession from J2000; coordinates in other epochs are returned as-is.
        /// </summary>
        public EquatorialCoordinate PrecessionCorrected(DateTimeOffset moment)
        {
            // If already in equinox-of-date (e.g. solar system bodies), skip precession
            if (Epoch != Constants.J2000Noon_UT)
                return new EquatorialCoordinate(RightAscension, Declination, moment);

            // Julian centuries since J2000.0
            var T = moment.Subtract(Constants.J2000Noon_UT).TotalDays / 36525.0;
            if (Math.Abs(T) > 40)
                throw new ArgumentOutOfRangeException(nameof(moment),
                    $"Lieske (1979) precession approximation is only valid within ~1000 years of J2000 and completely unusable over ~4000 years (got T={T:F1} centuries).");
            var T2 = T * T;
            var T3 = T2 * T;

            // Lieske (1979) precession angles in arcseconds, then convert to degrees
            var zetaA  = Angle.FromDegrees((2306.2181 * T + 0.30188 * T2 + 0.017998 * T3) / 3600.0);
            var zA     = Angle.FromDegrees((2306.2181 * T + 1.09468 * T2 + 0.018203 * T3) / 3600.0);
            var thetaA = Angle.FromDegrees((2004.3109 * T - 0.42665 * T2 - 0.041833 * T3) / 3600.0);

            // Rigorous scalar form of the precession rotation
            var raShifted = RightAscension + zetaA;
            var sinDec = sin(thetaA) * cos(Declination) * cos(raShifted) + cos(thetaA) * sin(Declination);
            var cosDecSinRA = cos(Declination) * Math.Sin((double)raShifted);
            var cosDecCosRA = cos(thetaA) * cos(Declination) * cos(raShifted) - sin(thetaA) * sin(Declination);

            var newRA = (Angle)Math.Atan2(cosDecSinRA, cosDecCosRA) + zA;
            var newDec = (Angle)Math.Asin(sinDec);

            return new EquatorialCoordinate(newRA, newDec, moment);
        }

        /// <summary>
        /// Calculates the alt/az coordinates on the specified time and observer location
        /// </summary>
        public HorizontalCoordinate GetHorizontalCoordinate(DateTimeOffset moment, WGS84Coordinate observer)
        {
            // Precess J2000 coordinates to equinox-of-date
            var precessed = PrecessionCorrected(moment);

            var longitude = observer.Longitude;
            var latitude = observer.Latitude;
            // Careful reading of http://en.wikipedia.org/wiki/Hour_angle#Relation_with_the_right_ascension
            var hourangle = moment.GreenwichMeanSiderialTime() + longitude - precessed.RightAscension;
            // Code adapted from http://en.wikipedia.org/wiki/Horizontal_coordinate_system#equatorial_to_horizontal 20120504
            // This is some sphere trigonometry
            var sinAlt = sin(latitude) * sin(precessed.Declination) + cos(latitude) * cos(precessed.Declination) * cos(hourangle);
            var cosAzCosAlt = cos(latitude) * sin(precessed.Declination) - sin(latitude) * cos(precessed.Declination) * cos(hourangle);
            var sinAzCosAlt = -cos(precessed.Declination) * sin(hourangle);

            var radius = Pythagoras(cosAzCosAlt, sinAzCosAlt);
            // Using Atan2 makes sure that the angle from the right quadrant is calculated
            var azimuth = (Angle)Math.Atan2(sinAzCosAlt, cosAzCosAlt);
            var altitude = (Angle)Math.Atan2(sinAlt, radius);

            // Atmospheric refraction: Sæmundsson (1986) formula
            // Adds a small positive correction so objects appear higher than geometric position
            altitude = ApplyRefraction(altitude);

            return new HorizontalCoordinate(altitude, azimuth);
        }

        /// <summary>
        /// Atmospheric refraction correction using Sæmundsson's formula.
        /// Input/output are true (geometric) and apparent altitude.
        /// Correction is ~0.6° at the horizon, ~0.1° at 10°, negligible above 45°.
        /// </summary>
        private static Angle ApplyRefraction(Angle geometricAltitude)
        {
            var altDeg = geometricAltitude.Degrees;
            if (altDeg < -1) return geometricAltitude; // below horizon, no correction
            // Sæmundsson (1986), cited in Meeus "Astronomical Algorithms"
            // R in arcminutes = 1.02 / tan(h + 10.3/(h + 5.11))
            var correction = 1.02 / Math.Tan((altDeg + 10.3 / (altDeg + 5.11)) * Math.PI / 180.0);
            return geometricAltitude + Angle.FromDegrees(correction / 60.0);
        }

        private static double Pythagoras(double x, double y)
        {
            return Math.Sqrt(x * x + y * y);
        }
    }
}
