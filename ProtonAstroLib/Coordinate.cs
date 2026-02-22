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

    public struct EquatorialCoordinate(Angle ra, Angle dec)
    {
        public readonly Angle RightAscention { get { return ra; } }
        public readonly Angle Declination { get { return dec; } }

        public Angle Distance(EquatorialCoordinate other)
        {
            var altdiff = (double)(RightAscention - other.RightAscention);
            var azdiff = (double)(Declination - other.Declination);
            return (Angle)Math.Sqrt(altdiff * altdiff + azdiff * azdiff);
        }


        // syntactic sugar
        static readonly Func<Angle, double> sin = a => Math.Sin((double)a);
        static readonly Func<Angle, double> cos = a => Math.Cos((double)a);

        // All wikipedia below is accessed 7 may 2012

        /// <summary>
        /// Calculates the alt/az coordinates on the specified time and observer location
        /// </summary>
        public HorizontalCoordinate GetHorizontalCoordinate(DateTimeOffset moment, WGS84Coordinate observer)
        {
            var longitude = observer.Longitude;
            var latitude = observer.Latitude;
            // Careful reading of http://en.wikipedia.org/wiki/Hour_angle#Relation_with_the_right_ascension
            var hourangle = moment.GreenwichMeanSiderialTime() + longitude - RightAscention;
            // Code adapted from http://en.wikipedia.org/wiki/Horizontal_coordinate_system#equatorial_to_horizontal 20120504
            // This is some sphere trigonometry
            var sinAlt = sin(latitude) * sin(Declination) + cos(latitude) * cos(Declination) * cos(hourangle);
            var cosAzCosAlt = cos(latitude) * sin(Declination) - sin(latitude) * cos(Declination) * cos(hourangle);
            var sinAzCosAlt = -cos(Declination) * sin(hourangle);

            var radius = Pythagoras(cosAzCosAlt, sinAzCosAlt);
            // Using Atan2 makes sure that the angle from the right quadrant is calculated
            var azimuth = (Angle)Math.Atan2(sinAzCosAlt, cosAzCosAlt);
            var altitude = (Angle)Math.Atan2(sinAlt, radius);

            return new HorizontalCoordinate(altitude, azimuth);
        }


        private static double Pythagoras(double x, double y)
        {
            return Math.Sqrt(x * x + y * y);
        }
    }
}
