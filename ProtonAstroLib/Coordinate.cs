using System;
using static System.Math;
using static ProtonAstroLib.Angle;

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
            return (Angle)Sqrt(altdiff * altdiff + azdiff * azdiff);
        }

        /// <summary>
        /// Inverse of EquatorialCoordinate.GetHorizontalCoordinate: converts a horizontal
        /// (alt/az) position back to equatorial (RA/Dec) for a given moment and observer.
        /// The result is in equinox-of-date (epoch = moment). Refraction is removed before
        /// converting, assuming the input altitude is apparent (refraction-corrected).
        /// </summary>
        public readonly EquatorialCoordinate ToEquatorialCoordinate(DateTimeOffset moment, WGS84Coordinate observer)
        {
            var latitude = observer.Latitude;
            var longitude = observer.Longitude;

            // Remove atmospheric refraction to get geometric altitude
            var alt = RemoveRefraction(Altitude);
            var az = Azimuth;

            // Inverse spherical trig (mirror of GetHorizontalCoordinate)
            // sin(dec) = sin(alt)*sin(lat) + cos(alt)*cos(lat)*cos(az)
            var sinDec = Sin(alt) * Sin(latitude) + Cos(alt) * Cos(latitude) * Cos(az);
            var dec = ArcSin(sinDec);

            // hour angle from atan2:
            //   numerator:   -cos(alt)*sin(az)          [same as sinAzCosAlt in forward]
            //   denominator:  sin(alt)*cos(lat) - cos(alt)*sin(lat)*cos(az)  [cosAzCosAlt in forward]
            var hourAngle = ArcTan(-Cos(alt) * Sin(az),
                                    Sin(alt) * Cos(latitude) - Cos(alt) * Sin(latitude) * Cos(az));

            // RA = GMST + longitude - HA  (inverse of HA = GMST + lon - RA)
            var ra = moment.GreenwichMeanSiderialTime() + longitude - hourAngle;

            return new EquatorialCoordinate(ra, dec, moment);
        }

        /// <summary>
        /// Inverse of Sæmundsson refraction: given an apparent (refraction-corrected) altitude,
        /// recover the geometric altitude by iterating the forward formula.
        /// </summary>
        private static Angle RemoveRefraction(Angle apparentAltitude)
        {
            var appDeg = apparentAltitude.Degrees;
            if (appDeg < -1) return apparentAltitude;
            // Iterate: geometric = apparent - R(geometric), starting from geometric ≈ apparent
            var geoDeg = appDeg;
            for (int i = 0; i < 3; i++)
            {
                var correction = 1.02 / Tan((geoDeg + 10.3 / (geoDeg + 5.11)) * PI / 180.0);
                geoDeg = appDeg - correction / 60.0;
            }
            return FromDegrees(geoDeg);
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
            return new EquatorialCoordinate(FromHMS(ra), FromDegrees(dec));
        }

        public readonly Angle RightAscension { get { return ra; } }
        public readonly Angle Declination { get { return dec; } }

        public Angle Distance(EquatorialCoordinate other)
        {
            var altdiff = (double)(RightAscension - other.RightAscension);
            var azdiff = (double)(Declination - other.Declination);
            return (Angle)Sqrt(altdiff * altdiff + azdiff * azdiff);
        }

        // All wikipedia below is accessed 7 may 2012

        /// <summary>
        /// Applies IAU 2000 precession (Capitaine et al. 2003) to precess from this coordinate's Epoch
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
            if (Abs(T) > 40)
                throw new ArgumentOutOfRangeException(nameof(moment),
                    $"IAU 2000 precession approximation is only valid within ~1000 years of J2000 and completely unusable over ~4000 years (got T={T:F1} centuries).");
            var T2 = T * T;
            var T3 = T2 * T;

            // IAU 2000 precession angles (Capitaine et al. 2003) in arcseconds, then convert to degrees
            // Constant terms are the frame bias between the dynamical equinox and the ICRS
            var zetaA  = FromDegrees((2.5976176 + 2306.0809506 * T + 0.3019015 * T2 + 0.0179663 * T3) / 3600.0);
            var zA     = FromDegrees((-2.5976176 + 2306.0803226 * T + 1.0947790 * T2 + 0.0182273 * T3) / 3600.0);
            var thetaA = FromDegrees((2004.1917476 * T - 0.4269353 * T2 - 0.0418251 * T3) / 3600.0);

            // Rigorous scalar form of the precession rotation
            var raShifted = RightAscension + zetaA;
            var sinDec = Sin(thetaA) * Cos(Declination) * Cos(raShifted) + Cos(thetaA) * Sin(Declination);
            var cosDecSinRA = Cos(Declination) * Sin(raShifted);
            var cosDecCosRA = Cos(thetaA) * Cos(Declination) * Cos(raShifted) - Sin(thetaA) * Sin(Declination);

            var newRA = ArcTan(cosDecSinRA, cosDecCosRA) + zA;
            var newDec = ArcSin(sinDec);

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
            var sinAlt = Sin(latitude) * Sin(precessed.Declination) + Cos(latitude) * Cos(precessed.Declination) * Cos(hourangle);
            var cosAzCosAlt = Cos(latitude) * Sin(precessed.Declination) - Sin(latitude) * Cos(precessed.Declination) * Cos(hourangle);
            var sinAzCosAlt = -Cos(precessed.Declination) * Sin(hourangle);

            var radius = Sqrt(cosAzCosAlt * cosAzCosAlt + sinAzCosAlt * sinAzCosAlt);
            // Using Atan2 makes sure that the angle from the right quadrant is calculated
            var azimuth = ArcTan(sinAzCosAlt, cosAzCosAlt);
            var altitude = ArcTan(sinAlt, radius);

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
            var correction = 1.02 / Tan((altDeg + 10.3 / (altDeg + 5.11)) * PI / 180.0);
            return geometricAltitude + FromDegrees(correction / 60.0);
        }
    }
}
