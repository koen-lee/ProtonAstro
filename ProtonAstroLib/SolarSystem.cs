using System;
using System.Collections.Generic;

namespace ProtonAstroLib
{
    /// <summary>
    /// Computes equatorial coordinates for Solar System bodies.
    /// Low-accuracy (~1°) planetary positions using Jean Meeus orbital elements (Table 31.a).
    /// Sufficient for visual pointing with a Dobsonian.
    /// </summary>
    public static class SolarSystem
    {
        private static readonly Dictionary<string, Func<DateTimeOffset, EquatorialCoordinate>> _bodies = new()
        {
            [nameof(Sun)]     = Sun,
            [nameof(Mercury)] = Mercury,
            [nameof(Venus)]   = Venus,
            [nameof(Mars)]    = Mars,
            [nameof(Jupiter)] = Jupiter,
            [nameof(Saturn)]  = Saturn,
            [nameof(Uranus)]  = Uranus,
        };

        /// <summary>
        /// Returns true and sets <paramref name="func"/> if <paramref name="name"/> is a known Solar System body.
        /// </summary>
        public static bool TryGetBody(string name, out Func<DateTimeOffset, EquatorialCoordinate> func)
            => _bodies.TryGetValue(name, out func!);

        // ── Sun ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the Sun's equatorial coordinates (equinox-of-date) for a given moment.
        /// Low-accuracy solar position (~0.01°) using the algorithm from
        /// the Astronomical Almanac, sufficient for pointing a Dobson or solar oven.
        /// </summary>
        public static EquatorialCoordinate Sun(DateTimeOffset moment)
        {
            var D = moment.Subtract(Constants.J2000Noon_UT).TotalDays;

            var M = Angle.FromDegrees(357.5291 + 0.98560028 * D);
            var C = Angle.FromDegrees(1.9148) * Angle.Sin(M)
                  + Angle.FromDegrees(0.0200) * Angle.Sin(M * 2)
                  + Angle.FromDegrees(0.0003) * Angle.Sin(M * 3);
            // Ecliptic longitude: mean anomaly + center + perihelion longitude + 180°
            var lambda = M + C + Angle.FromDegrees(282.9372);

            var epsilon = Angle.FromDegrees(23.4393 - 0.0000004 * D);

            // Ecliptic to equatorial (ecliptic latitude β ≈ 0 for the Sun)
            var sinLambda = Angle.Sin(lambda);
            var cosLambda = Angle.Cos(lambda);
            var ra  = (Angle)Math.Atan2(Angle.Cos(epsilon) * sinLambda, cosLambda);
            var dec = (Angle)Math.Asin(Angle.Sin(epsilon) * sinLambda);

            return new EquatorialCoordinate(ra, dec, moment);
        }

        // ── Planets ──────────────────────────────────────────────────────────
        // Orbital elements from Meeus, Astronomical Algorithms, Table 31.a.
        // Arguments: mean longitude (L0, L1 deg/cy), semi-major axis (AU),
        //            eccentricity (e0, e1 /cy), inclination (i0, i1 deg/cy),
        //            ascending node (O0, O1 deg/cy), perihelion longitude (w0, w1 deg/cy).

        public static EquatorialCoordinate Mercury(DateTimeOffset moment) =>
            ComputePlanet(moment,
                L0: 252.2509, L1: 149472.6749,
                a: 0.387098,
                e0: 0.20563,  e1:  0.000020,
                i0: 7.0050,   i1:  0.0018,
                O0: 48.3310,  O1:  1.1861,
                w0: 77.4561,  w1:  0.1590);

        public static EquatorialCoordinate Venus(DateTimeOffset moment) =>
            ComputePlanet(moment,
                L0: 181.9798, L1: 58517.8157,
                a: 0.723330,
                e0: 0.006773, e1: -0.000048,
                i0: 3.3947,   i1:  0.0010,
                O0: 76.6799,  O1:  0.9011,
                w0: 131.5637, w1:  0.0019);

        public static EquatorialCoordinate Mars(DateTimeOffset moment) =>
            ComputePlanet(moment,
                L0: 355.4330, L1: 19140.2993,
                a: 1.523688,
                e0: 0.093405, e1:  0.000092,
                i0: 1.8497,   i1: -0.0016,
                O0: 49.5581,  O1:  0.7720,
                w0: 336.0600, w1:  0.4438);

        public static EquatorialCoordinate Jupiter(DateTimeOffset moment) =>
            ComputePlanet(moment,
                L0:  34.3515, L1: 3034.9057,
                a: 5.202561,
                e0: 0.048498, e1:  0.000163,
                i0: 1.3030,   i1: -0.0019,
                O0: 100.4644, O1:  1.0209,
                w0:  14.3312, w1:  1.6132);

        public static EquatorialCoordinate Saturn(DateTimeOffset moment) =>
            ComputePlanet(moment,
                L0:  49.9443, L1: 1222.1138,
                a: 9.554909,
                e0: 0.055548, e1: -0.000346,
                i0: 2.4886,   i1: -0.0021,
                O0: 113.6655, O1:  0.8771,
                w0:  93.0568, w1:  1.9637);

        public static EquatorialCoordinate Uranus(DateTimeOffset moment) =>
            ComputePlanet(moment,
                L0: 313.2330, L1: 428.4814,
                a: 19.21814,
                e0: 0.046307, e1: -0.000011,
                i0: 0.7733,   i1:  0.0019,
                O0:  74.0060, O1:  0.5291,
                w0: 173.0050, w1:  1.4863);

        // ── Helpers ──────────────────────────────────────────────────────────

        private static EquatorialCoordinate ComputePlanet(
            DateTimeOffset moment,
            double L0, double L1,
            double a,
            double e0, double e1,
            double i0, double i1,
            double O0, double O1,
            double w0, double w1)
        {
            var D = moment.Subtract(Constants.J2000Noon_UT).TotalDays;
            var T = D / 36525.0;

            var epsilon = (23.4393 - 0.0000004 * D) * Deg2Rad;

            // Heliocentric position of the planet
            var (lp, bp, rp) = Heliocentric(L0, L1, a, e0, e1, i0, i1, O0, O1, w0, w1, T);

            // Heliocentric position of the Earth (defines the geocentric conversion)
            var (le, _, re) = Heliocentric(
                L0: 100.4664, L1: 35999.3720,
                a: 1.000000,
                e0: 0.016709, e1: -0.000042,
                i0: 0.0,      i1:  0.0,
                O0: 0.0,      O1:  0.0,
                w0: 102.9373, w1:  0.3198,
                T);

            // Heliocentric → geocentric rectangular ecliptic coordinates
            var X = rp * Math.Cos(bp) * Math.Cos(lp) - re * Math.Cos(le);
            var Y = rp * Math.Cos(bp) * Math.Sin(lp) - re * Math.Sin(le);
            var Z = rp * Math.Sin(bp);

            // Geocentric ecliptic longitude and latitude
            var lambda = Math.Atan2(Y, X);
            var beta   = Math.Atan2(Z, Math.Sqrt(X * X + Y * Y));

            // Ecliptic → equatorial
            var cosEps = Math.Cos(epsilon);
            var sinEps = Math.Sin(epsilon);
            var raRad = Math.Atan2(Math.Sin(lambda) * cosEps - Math.Tan(beta) * sinEps, Math.Cos(lambda));
            if (raRad < 0) raRad += 2 * Math.PI;
            var ra  = (Angle)raRad;
            var dec = (Angle)Math.Asin(Math.Sin(beta) * cosEps + Math.Cos(beta) * sinEps * Math.Sin(lambda));

            return new EquatorialCoordinate(ra, dec, moment);
        }

        /// <summary>
        /// Computes heliocentric ecliptic longitude, latitude (radians), and distance (AU)
        /// from mean orbital elements using a truncated equation-of-center series.
        /// </summary>
        private static (double l, double b, double r) Heliocentric(
            double L0, double L1,
            double a,
            double e0, double e1,
            double i0, double i1,
            double O0, double O1,
            double w0, double w1,
            double T)
        {
            var e = e0 + e1 * T;
            var i = (i0 + i1 * T) * Deg2Rad;
            var O = (O0 + O1 * T) * Deg2Rad;
            var w = (w0 + w1 * T) * Deg2Rad;  // longitude of perihelion
            var L = (L0 + L1 * T) * Deg2Rad;  // mean longitude

            var M = L - w;  // mean anomaly

            // Equation of center — truncated power series in e (~1° accuracy)
            var C = (2 * e - e * e * e / 4.0) * Math.Sin(M)
                  + (5.0 / 4 * e * e)          * Math.Sin(2 * M)
                  + (13.0 / 12 * e * e * e)    * Math.Sin(3 * M);

            var nu = M + C;       // true anomaly
            var l  = nu + w;      // heliocentric ecliptic longitude
            var b  = Math.Asin(Math.Sin(i) * Math.Sin(l - O));  // heliocentric ecliptic latitude
            var r  = a * (1 - e * e) / (1 + e * Math.Cos(nu)); // heliocentric distance (AU)

            return (l, b, r);
        }

        private const double Deg2Rad = Math.PI / 180.0;
    }
}
