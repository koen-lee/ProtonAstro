using System;
using Xunit;
using Xunit.Abstractions;

namespace ProtonAstroLib.Tests
{
    public class DiagnosticTests(ITestOutputHelper output)
    {
        private static readonly WGS84Coordinate Maassluis = new(
            Angle.FromDegrees(51, 55, 11.99),
            Angle.FromDegrees(4, 15, 36.00));

        [Fact]
        public void PrintActualErrors()
        {
            // 1. North pole at earth pole, J2000
            var northpole = new EquatorialCoordinate((Angle)13.7, Angle.FromDegrees(90.0));
            var pole = new WGS84Coordinate(Angle.FromDegrees(90), (Angle)34.2);
            var r1 = northpole.GetHorizontalCoordinate(Constants.J2000Epoch, pole);
            output.WriteLine($"NorthPole@Pole J2000: alt={r1.Altitude.Degrees:F6}° (error from 90: {90 - r1.Altitude.Degrees:F6}°)");

            // 2. North pole at earth pole, 2012
            var moment2012 = new DateTimeOffset(2012, 5, 7, 23, 20, 12, TimeSpan.FromHours(2));
            var r2 = northpole.GetHorizontalCoordinate(moment2012, pole);
            output.WriteLine($"NorthPole@Pole 2012:  alt={r2.Altitude.Degrees:F6}° (error from 90: {90 - r2.Altitude.Degrees:F6}°)");

            // 3. North pole at Maassluis, J2000
            var r3 = northpole.GetHorizontalCoordinate(Constants.J2000Epoch, Maassluis);
            output.WriteLine($"NorthPole@Maassluis J2000: alt={r3.Altitude.Degrees:F6}° (expected {Maassluis.Latitude.Degrees:F6}°, diff={Maassluis.Latitude.Degrees - r3.Altitude.Degrees:F6}°) az={r3.Azimuth.Degrees:F6}°");

            // 4. Vega at J2000
            var vega = new EquatorialCoordinate(Angle.FromTime(new TimeSpan(18, 36, 56)), Angle.FromDegrees(38, 47, 3));
            var r4 = vega.GetHorizontalCoordinate(Constants.J2000Epoch, Maassluis);
            var expectedAz4 = Angle.FromDegrees(197, 29, 13);
            var expectedAlt4 = Angle.FromDegrees(76, 22, 16);
            output.WriteLine($"Vega@Maassluis J2000: az={r4.Azimuth.Degrees:F4}° (expected {expectedAz4.Degrees:F4}°, diff={r4.Azimuth.Degrees - expectedAz4.Degrees:F4}°) alt={r4.Altitude.Degrees:F4}° (expected {expectedAlt4.Degrees:F4}°, diff={r4.Altitude.Degrees - expectedAlt4.Degrees:F4}°)");

            // 5. Vega at 2012
            var r5 = vega.GetHorizontalCoordinate(moment2012, Maassluis);
            var expectedAz5 = Angle.FromDegrees(64, 19, 06);
            var expectedAlt5 = Angle.FromDegrees(30, 09, 21);
            output.WriteLine($"Vega@Maassluis 2012:  az={r5.Azimuth.Degrees:F4}° (expected {expectedAz5.Degrees:F4}°, diff={r5.Azimuth.Degrees - expectedAz5.Degrees:F4}°) alt={r5.Altitude.Degrees:F4}° (expected {expectedAlt5.Degrees:F4}°, diff={r5.Altitude.Degrees - expectedAlt5.Degrees:F4}°)");

            var gammaCep = new EquatorialCoordinate(
                Angle.FromHMS("23h39m20s"),
                Angle.FromDegrees(77, 37, 56.51));

            var j2000Dec = gammaCep.Declination.Degrees;
            var year2500 = new DateTimeOffset(2500, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var precessed = gammaCep.PrecessionCorrected(year2500);
            output.WriteLine($"Gamma Cephei declination: J2000={j2000Dec:F6}°, 2500 AD={precessed.Declination.Degrees:F6}° (delta={precessed.Declination.Degrees - j2000Dec:F6}°)");
            var precessed2 = gammaCep.PrecessionCorrected(Constants.J2000Epoch.AddYears(2000));
            output.WriteLine($"Gamma Cephei declination: J2000={j2000Dec:F6}°, 4000 AD={precessed2.Declination.Degrees:F6}° (delta={precessed2.Declination.Degrees - j2000Dec:F6}°)");
            }
    }
}
