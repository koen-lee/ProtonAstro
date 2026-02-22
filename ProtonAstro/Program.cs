using System;
using ProtonAstroLib;

namespace ProtonAstro
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var vega = new EquatorialCoordinate(Angle.FromTime(new TimeSpan(18, 36, 56)), Angle.FromDegrees(38, 47, 3));
            var now = DateTimeOffset.Now;
            var next = DateTimeOffset.Now.AddSeconds(1);
            var maassluis = new WGS84Coordinate(Angle.FromDegrees(51, 55, 11.99), Angle.FromDegrees(4, 15, 36.00));
            var altaz = vega.GetHorizontalCoordinate(now, maassluis);
            Console.WriteLine($"Vega is currently at altitude {altaz.Altitude} and azimuth {altaz.Azimuth}");
            var altaz2 = vega.GetHorizontalCoordinate(next, maassluis);
            Console.WriteLine($"Vega is moving at {(altaz2.Altitude - altaz.Altitude).Degrees} deg/sec alt and {(altaz2.Azimuth - altaz.Azimuth).Degrees} deg/sec az");

        }
    }
}
