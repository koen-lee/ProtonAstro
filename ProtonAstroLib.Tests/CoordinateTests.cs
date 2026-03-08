using System;
using Xunit;

namespace ProtonAstroLib.Tests
{
    public class CoordinateTests
    {
        private static readonly WGS84Coordinate Maassluis = new(
            Angle.FromDegrees(51, 55, 11.99),
            Angle.FromDegrees(4, 15, 36.00));

        [Fact]
        public void NorthPole_AtEarthPole_OnJ2000_AltitudeIs90()
        {
            // At J2000 epoch, precession is zero so the geometric identity holds exactly.
            // Refraction at 90° altitude is negligible (~0.0001°).
            var northpole = new EquatorialCoordinate((Angle)13.7, Angle.FromDegrees(90.0));
            var pole = new WGS84Coordinate(Angle.FromDegrees(90), (Angle)34.2);

            var result = northpole.GetHorizontalCoordinate(Constants.J2000Epoch, pole);

            Assert.Equal(90.0, result.Altitude.Degrees, 3);
        }

        [Fact]
        public void NorthPole_AtEarthPole_In2012_AltitudeIsNear90()
        {
            // 12 years from J2000: precession shifts the J2000 pole by ~0.07°
            var northpole = new EquatorialCoordinate((Angle)13.7, Angle.FromDegrees(90.0));
            var moment = new DateTimeOffset(2012, 5, 7, 23, 20, 12, TimeSpan.FromHours(2));
            var pole = new WGS84Coordinate(Angle.FromDegrees(90), (Angle)34.2);

            var result = northpole.GetHorizontalCoordinate(moment, pole);

            AssertAngleEqual(Angle.FromDegrees(90), result.Altitude, toleranceDegrees: 0.1);
        }

        [Fact]
        public void NorthPole_AtMaassluis_OnJ2000_AltitudeEqualsLatitude()
        {
            var northpole = new EquatorialCoordinate((Angle)13.7, Angle.FromDegrees(90.0));

            var result = northpole.GetHorizontalCoordinate(Constants.J2000Epoch, Maassluis);

            Assert.Equal(Maassluis.Latitude.Degrees, result.Altitude.Degrees, 1);
            Assert.Equal(0.0, result.Azimuth.Degrees, 0);
        }

        [Fact]
        public void NorthPole_AtMaassluis_In2012_AltitudeNearLatitude()
        {
            var northpole = new EquatorialCoordinate((Angle)13.7, Angle.FromDegrees(90.0));
            var moment = new DateTimeOffset(2012, 5, 7, 23, 20, 12, TimeSpan.FromHours(2));

            var result = northpole.GetHorizontalCoordinate(moment, Maassluis);

            AssertAngleEqual(Maassluis.Latitude, result.Altitude, toleranceDegrees: 0.1);
        }
        
        [Fact]
        public void NorthPole_AtMaassluis_In2026_AltitudeNearLatitude()
        {
            var northpole = new EquatorialCoordinate((Angle)13.7, Angle.FromDegrees(90.0));
            var moment = new DateTimeOffset(2026, 2, 22, 20, 44, 12, TimeSpan.FromHours(2));

            var result = northpole.GetHorizontalCoordinate(moment, Maassluis);

            AssertAngleEqual(Maassluis.Latitude, result.Altitude, toleranceDegrees: 0.1);
        }

        [Fact]
        public void Vega_OnEpoch_CorrectHorizontalCoordinate()
        {
            var vega = new EquatorialCoordinate(Angle.FromTime(new TimeSpan(18, 36, 56)), Angle.FromDegrees(38, 47, 3));
            var moment = Constants.J2000Epoch;

            var result = vega.GetHorizontalCoordinate(moment, Maassluis);

            // expected values from Stellarium
            AssertAngleEqual(Angle.FromDegrees(197, 29, 13), result.Azimuth);
            AssertAngleEqual(Angle.FromDegrees(76, 22, 16), result.Altitude);
        }

        [Fact]
        public void Vega_AtSpecificTime_CorrectHorizontalCoordinate()
        {
            var vega = new EquatorialCoordinate(Angle.FromTime(new TimeSpan(18, 36, 56)), Angle.FromDegrees(38, 47, 3));
            var moment = new DateTimeOffset(2012, 5, 7, 23, 20, 12, TimeSpan.FromHours(2));

            var result = vega.GetHorizontalCoordinate(moment, Maassluis);

            // expected values from Stellarium
            AssertAngleEqual(Angle.FromDegrees(64, 19, 06), result.Azimuth);
            AssertAngleEqual(Angle.FromDegrees(30, 09, 21), result.Altitude);
        }

        [Fact]
        public void GammaCephei_PrecessionMovesItPoleward()
        {
            // Gamma Cephei (Errai) - J2000 coordinates: RA 23h39m20.910s, Dec +77°37'56.51"
            // Precession is carrying it toward the celestial pole (it becomes pole star ~4000 AD).
            // The Lieske polynomials are only valid for a few centuries, so we test at +500 years.
            var gammaCep = new EquatorialCoordinate(
                Angle.FromHMS("23h39m20s"),
                Angle.FromDegrees(77, 37, 56.51));

            var j2000Dec = gammaCep.Declination.Degrees;
            var year2500 = new DateTimeOffset(4094, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var precessed = gammaCep.PrecessionCorrected(year2500);

            Assert.True(precessed.Declination.Degrees > 87,
                $"Expected Gamma Cephei to move poleward by 4094, but declination went from {j2000Dec:F2}° to {precessed.Declination.Degrees:F2}°");
        }

        [Fact]
        public void Precession_RejectsDatesBeyondUsableYears()
        {
            var star = new EquatorialCoordinate((Angle)0, Angle.FromDegrees(45));
            var tooFar = new DateTimeOffset(6600, 1, 1, 12, 0, 0, TimeSpan.Zero);

            Assert.Throws<ArgumentOutOfRangeException>(() => star.PrecessionCorrected(tooFar));
        }

        [Fact]
        public void Sun_OnSummerSolstice_DeclinationIsPlus23()
        {
            // 2024 summer solstice: June 20 20:51 UTC
            var solstice = new DateTimeOffset(2024, 6, 20, 20, 51, 0, TimeSpan.Zero);
            var sun = SolarSystem.Sun(solstice);

            Assert.Equal(23.44, sun.Declination.Degrees, 0.5);
        }

        [Fact]
        public void Sun_OnWinterSolstice_DeclinationIsMinus23()
        {
            // 2024 winter solstice: December 21 09:20 UTC
            var solstice = new DateTimeOffset(2024, 12, 21, 9, 20, 0, TimeSpan.Zero);
            var sun = SolarSystem.Sun(solstice);

            Assert.Equal(-23.44, sun.Declination.Degrees, 0.5);
        }

        [Fact]
        public void Sun_OnVernalEquinox_DeclinationIsZero()
        {
            // 2024 vernal equinox: March 20 03:06 UTC
            var equinox = new DateTimeOffset(2024, 3, 20, 3, 6, 0, TimeSpan.Zero);
            var sun = SolarSystem.Sun(equinox);

            Assert.Equal(0, sun.Declination.Degrees, 0.5);
        }

        [Fact]
        public void Sun_AtSolarNoon_IsDueSouth()
        {
            // At solar noon the Sun should be due south (azimuth ~180°) from Maassluis.
            // Solar noon ≈ 12:00 UTC - longitude/15 hours. Maassluis is at ~4.26°E,
            // so solar noon ≈ 11:43 UTC. On the equinox, noon sun altitude ≈ 90° - latitude ≈ 38°.
            var equinox = new DateTimeOffset(2024, 3, 20, 11, 43, 0, TimeSpan.Zero);
            var sun = SolarSystem.Sun(equinox);
            var result = sun.GetHorizontalCoordinate(equinox, Maassluis);

            AssertAngleEqual(Angle.FromDegrees(180), result.Azimuth, toleranceDegrees: 2);
            // Altitude at equinox solar noon ≈ 90 - 51.92 = 38.08°
            AssertAngleEqual(Angle.FromDegrees(38), result.Altitude, toleranceDegrees: 1);
        }

        [Fact]
        public void Sun_SkipsPrecession()
        {
            // Sun coordinates are equinox-of-date, so PrecessionCorrected should be a no-op
            var moment = new DateTimeOffset(2024, 6, 20, 12, 0, 0, TimeSpan.Zero);
            var sun = SolarSystem.Sun(moment);
            var precessed = sun.PrecessionCorrected(moment);

            Assert.Equal(sun.RightAscension.Degrees, precessed.RightAscension.Degrees, 10);
            Assert.Equal(sun.Declination.Degrees, precessed.Declination.Degrees, 10);
        }

        private static void AssertAngleEqual(Angle expected, Angle actual, double toleranceDegrees = 0.05)
        {
            Assert.True(Math.Abs((expected - actual).Degrees) < toleranceDegrees,
                $"Expected {expected} but got {actual}, difference is {(expected - actual).Degrees:F4} degrees (tolerance: {toleranceDegrees}°).");
        }
    }
}
