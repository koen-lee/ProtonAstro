using System.Globalization;

namespace TelescopeDrive.Models;

public record GCodeCommand(string Command, string? Description = null)
{
    public static GCodeCommand AbsoluteMove(double altDeg, double azDeg) =>
        new($"G90\nG0 X{F(altDeg)} Y{F(azDeg)}", $"Move to alt={altDeg:F4} az={azDeg:F4}");

    public static GCodeCommand TrackedMove(double altDeg, double azDeg, double feedrateDegPerMin) =>
        new($"G90\nG1 X{F(altDeg)} Y{F(azDeg)} F{F(feedrateDegPerMin)}", $"Track to alt={altDeg:F4} az={azDeg:F4} F={feedrateDegPerMin:F4}");

    public static GCodeCommand RelativeMove(double dAlt, double dAz) =>
        new($"G91\nG0 X{F(dAlt)} Y{F(dAz)}\nG90", $"Jog dAlt={dAlt:F4} dAz={dAz:F4}");

    public static GCodeCommand HomeX => new("G28 X", "Home altitude axis");
    public static GCodeCommand HomeY => new("G28 Y", "Home azimuth axis");
    public static GCodeCommand HomeAll => new("G28", "Home all axes");

    public static GCodeCommand SetPosition(double altDeg, double azDeg) =>
        new($"G92 X{F(altDeg)} Y{F(azDeg)}", $"Set position to alt={altDeg:F4} az={azDeg:F4}");

    public static GCodeCommand Raw(string gcode) => new(gcode, "Manual command");

    private static string F(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
}
