using ProtonAstroLib;

namespace TelescopeDrive.Models;

public class ObserverConfig
{
    public double LatitudeDegrees { get; set; } = 51.92;
    public double LongitudeDegrees { get; set; } = 4.26;
    public string SerialPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 115200;
    public int TrackingIntervalMs { get; set; } = 5000;

    public WGS84Coordinate ToWGS84() =>
        new(Angle.FromDegrees(LatitudeDegrees), Angle.FromDegrees(LongitudeDegrees));
}
