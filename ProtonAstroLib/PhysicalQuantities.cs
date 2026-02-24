using System;
using System.Text.RegularExpressions;
using static System.Math;

namespace ProtonAstroLib
{

    /// <summary>
    /// A length or distance; default unit is in meters.
    /// </summary>
    public struct Distance
    {
        private double value;

        public static explicit operator double(Distance d)
        {
            return d.value;
        }

        public static explicit operator Distance(double d)
        {
            return new Distance { value = d };
        }

        public double LightYears { get { return value / (double)Constants.LightYear; } }
        public double Parsecs { get { return value / (double)Constants.Parsec; } }
        public double AUs { get { return value / (double)Constants.AU; } }

        public static Distance FromParsecs(double parsecs) { return parsecs * Constants.Parsec; }
        public static Distance FromLightyears(double lightyears) { return lightyears * Constants.LightYear; }
        public static Distance FromAU(double AUs) { return Constants.AU * AUs; }


        public static Distance operator -(Distance a) => (Distance)(-a.value);
        public static Distance operator +(Distance a, Distance b) => (Distance)(a.value + b.value);
        public static Distance operator -(Distance a, Distance b) => (Distance)(a.value - b.value);
        public static Distance operator *(Distance a, double b) => (Distance)(a.value * b);
        public static Distance operator *(double b, Distance a) => (Distance)(a.value * b);
        public static Distance operator /(Distance a, double b) => (Distance)(a.value / b);

    }

    /// <summary>
    /// A geometric angle, default representation in radians
    /// </summary>
    public struct Angle : IComparable<Angle>, IFormattable
    {
        private double value;

        public static explicit operator double(Angle d)
        {
            return d.value;
        }

        public static explicit operator Angle(double d)
        {
            return new Angle { value = d };
        }

        public static double Sin(Angle a) { return Math.Sin(a.value); }
        public static double Cos(Angle a) { return Math.Cos(a.value); }
        public static double Tan(Angle a) { return Math.Tan(a.value); }

        public static Angle ArcSin(double sin) { return (Angle)Asin(sin); }
        public static Angle ArcCos(double cos) { return (Angle)Acos(cos); }
        public static Angle ArcTan(double y, double x) { return (Angle)Atan2(y, x); }
        public static Angle ArcTan(double x) { return (Angle)Atan(x); }
        /// <summary>
        /// Returns the angle as between 0, inclusive, and 2Pi, exclusive.
        /// Throws InvalidOperationException on degenerate doubles (NaN, inf)
        /// </summary>
        public Angle Normalized
        {
            get
            {
                if (double.IsInfinity(value)) throw new InvalidOperationException();
                if (double.IsNaN(value)) throw new InvalidOperationException();
                const double circle = 2 * PI;
                var result = value % circle;
                if (result < 0) result += circle;
                return new Angle() { value = result };
            }
        }

        public double Degrees { get { return 360 * value / (2 * PI); } }
        public static Angle FromDegrees(double value) { return (Angle)(2 * PI * (value / 360)); }

        /// <summary>
        /// The angle as hours, minutes, seconds in a 24-hour clock
        /// </summary>
        public TimeSpan Time { get { return TimeSpan.FromHours(Degrees / (360 / 24)); } }

        /// <summary>
        /// Returns the angle normalized to the range ±180° (±12h), which is useful for angle differences like hour angle or equation of time.
        /// Throws InvalidOperationException on degenerate doubles (NaN, inf)
        /// </summary>
        public Angle SymmetricNormalized
        {
            get
            {
                if (double.IsInfinity(value)) throw new InvalidOperationException();
                if (double.IsNaN(value)) throw new InvalidOperationException();
                const double circle = 2 * PI;
                var result = value % circle;
                if (result < -PI) result += circle;
                if (result >= PI) result -= circle;
                return new Angle() { value = result };
            }
        }
        /// <summary>
        /// The angle of the time using a 24-hour clock; 1h = 15 degrees
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static Angle FromTime(TimeSpan time) { return FromDegrees(time.TotalHours * (360 / 24)); }

        public static Angle operator -(Angle a) => (Angle)(-a.value);
        public static Angle operator +(Angle a, Angle b) => (Angle)(a.value + b.value);
        public static Angle operator -(Angle a, Angle b) => (Angle)(a.value - b.value);
        public static Angle operator *(Angle a, double b) => (Angle)(a.value * b);
        public static Angle operator *(double b, Angle a) => (Angle)(a.value * b);
        public static Angle operator /(Angle a, double b) => (Angle)(a.value / b);
        public static bool operator >(Angle a, Angle b) => a.value > b.value;
        public static bool operator <(Angle a, Angle b) => a.value < b.value;

        public static Angle FromDegrees(int degrees, int minutes, double seconds)
        {
            return FromDegrees(degrees + minutes / 60.0 + seconds / 3600.0);
        }

        public override string ToString()
        {
            return Degrees.ToString() + "deg";
        }

        /// <summary>
        /// Formats the angle using the specified format string. Supported formats are:
        /// DMS: degrees, minutes, seconds (e.g. 12d34m56s)
        /// HMS: hours, minutes, seconds (e.g. 12h34m56s)
        /// N{format}: normalized angle in degrees, using the specified format for the number (e.g. N0 for no decimals, N2 for 2 decimals, etc.)
        /// {format}: angle in degrees, using the specified format for the number (e.g. F2 for 2 decimals, etc.)
        /// </summary>
        /// <param name="format"></param>
        /// <param name="formatProvider"></param>
        /// <returns></returns>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format))
                return ToString();
            if (format == "DMS")
                return ToDMSString();
            if (format.StartsWith("HMS"))
                return Time.ToString(format[3..], formatProvider);
            if (format.StartsWith('N'))
                return Normalized.ToString(format[1..], formatProvider);
            if (format.StartsWith('D'))
                return Degrees.ToString(format[1..], formatProvider) + "°";
            return value.ToString(format, formatProvider);
        }

        public string ToDMSString()
        {
            var deg = Normalized.Degrees;
            var D = Floor(deg);            //degrees
            var M = Floor(60 * (deg - D)); //minutes
            var S = 60 * 60 * (deg - D - (M / 60));    //seconds
            return string.Format("{0}d{1:00}m{2:0.0}s", D, M, S);
        }

        public int CompareTo(Angle other)
        {
            return this.value.CompareTo(other.value);
        }

        private static Regex DMSRegex = new Regex(@"^(-?\d+)(?:d|°)\s*(\d+)?(?:m|')?\s*(\d+(?:\.\d+)?)?(?:s|"")?$");
        public static Angle FromDegrees(string degrees)
        {
            var match = DMSRegex.Match(degrees);
            if (!match.Success)
            {
                if (double.TryParse(degrees, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                    return FromDegrees(d);
                throw new FormatException($"Invalid angle format: {degrees}");
            }
            var D = int.Parse(match.Groups[1].Value);
            var M = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var S = match.Groups[3].Success ? double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
            return Sign(D) * FromDegrees(Abs(D), M, S);
        }

        private static Regex HMSRegex = new Regex(@"^(\d+)(?:[hH])\s*(\d+)?(?:[mM])?\s*(\d+(?:\.\d+)?)?(?:[sS])?$");
        public static Angle FromHMS(string hms)
        {
            var match = HMSRegex.Match(hms);
            if (!match.Success)
            {
                if (TimeSpan.TryParse(hms, out var d))
                    return FromTime(d);
                throw new FormatException($"Invalid angle format: {hms}");
            }
            var H = int.Parse(match.Groups[1].Value);
            var M = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var S = match.Groups[3].Success ? double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
            return FromTime(new TimeSpan(hours: H, minutes: M, seconds: 0).Add(TimeSpan.FromSeconds(S)));
        }
    }

    /// <summary>
    /// A mass; default unit is in kg.
    /// </summary>
    public struct Mass
    {
        private double value;

        public static implicit operator double(Mass d)
        {
            return d.value;
        }

        public static implicit operator Mass(double d)
        {
            return new Mass { value = d };
        }
    }

    public static class Constants
    {
        //General  constants
        public static Distance AU = (Distance)149597900000;                //AU in m
        public static Distance Parsec = 3.261633 * LightYear;                //Parsecs in light year
        public static double cLightSpeed = 299792500;           //Light speed in m/s
        public static Distance LightYear = (Distance)9.46053E+15;          //Light year (m)
        public static double cG = 0.0000000000667;              //Gravitational constant

        /// <summary>
        /// J2000.0 epoch: January 1, 2000, 12:00:00 TT = 11:58:55.816 UTC.
        /// </summary>
        public static DateTimeOffset J2000Epoch = new DateTimeOffset(2000, 1, 1, 11, 58, 55, 816, TimeSpan.Zero);

        /// <summary>
        /// The GMST formula's D=0 reference: noon UT (≈ noon UTC), not noon TT.
        /// The ~64s difference between TT and UT causes ~0.27° error in hour angle if J2000Epoch is used instead.
        /// </summary>
        public static DateTimeOffset J2000Noon_UT = new DateTimeOffset(2000, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
