using System;
using Xunit;

namespace ProtonAstroLib.Tests
{
    public class CoordinateTests
    {
        private static readonly Angle MaassluisLat = Angle.FromDegrees(51, 55, 11.99);
        private static readonly Angle MaassluisLon = -Angle.FromDegrees(4, 15, 36.00);

        [Fact]
        public void NorthPole_AtEarthPole_AltitudeIs90()
        {
            var northpole = new EquatorialCoordinate((Angle)13.7, Angle.FromDegrees(90.0));
            var moment = new DateTimeOffset(2012, 5, 7, 23, 20, 12, TimeSpan.FromHours(2));

            var result = northpole.GetHorizontalCoordinate(moment, (Angle)34.2, Angle.FromDegrees(90));

            Assert.Equal(90.0, result.Altitude.Degrees, 12);
        }

        [Fact]
        public void NorthPole_AtMaassluis_AltitudeEqualsLatitude()
        {
            var northpole = new EquatorialCoordinate((Angle)13.7, Angle.FromDegrees(90.0));
            var moment = new DateTimeOffset(2012, 5, 7, 23, 20, 12, TimeSpan.FromHours(2));

            var result = northpole.GetHorizontalCoordinate(moment, MaassluisLon, MaassluisLat);

            AssertAngleEqual(MaassluisLat, result.Altitude);
            Assert.Equal(0.0, result.Azimuth.Degrees, 0);
        }

        [Fact]
        public void Vega_OnEpoch_CorrectHorizontalCoordinate()
        {
            var vega = new EquatorialCoordinate(Angle.FromTime(new TimeSpan(18, 36, 56)), Angle.FromDegrees(38, 47, 3));
            var moment = Constants.J2000Epoch;

            var result = vega.GetHorizontalCoordinate(moment, MaassluisLon, MaassluisLat);

            // expected values from Stellarium
            AssertAngleEqual(Angle.FromDegrees(197, 29, 13), result.Azimuth);
            AssertAngleEqual(Angle.FromDegrees(76, 22, 16), result.Altitude);
        }

        [Fact]
        public void Vega_AtSpecificTime_CorrectHorizontalCoordinate()
        {
            var vega = new EquatorialCoordinate(Angle.FromTime(new TimeSpan(18, 36, 56)), Angle.FromDegrees(38, 47, 3));
            var moment = new DateTimeOffset(2012, 5, 7, 23, 20, 12, TimeSpan.FromHours(2));

            var result = vega.GetHorizontalCoordinate(moment, MaassluisLon, MaassluisLat);

            // expected values from Stellarium
            AssertAngleEqual(Angle.FromDegrees(64, 19, 06), result.Azimuth);
            AssertAngleEqual(Angle.FromDegrees(30, 09, 21), result.Altitude);
        }

        private static void AssertAngleEqual(Angle expected, Angle actual)
        {
            Assert.True(Math.Abs((expected - actual).Degrees) < 1,
                $"Expected {expected} but got {actual}");
        }
    }
}
