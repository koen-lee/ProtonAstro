using System.Globalization;

namespace TelescopeDrive.Models;

public record GCodeCommand(string Command, string? Description = null)
{
    /// <summary>GCode opcode constants shared with the simulator and any GCode parsers.</summary>
    public static class Op
    {
        public const string AbsoluteMode = "G90";
        public const string RelativeMode = "G91";
        public const string RapidMove    = "G0";
        public const string LinearMove   = "G1";
        public const string SetPosition  = "G92";
        public const string Home         = "G28";
        public const string QuickStop    = "M410";
        public const string WaitForMoves = "M400";
        public const string QueryPosition = "M114";
        public const string QueryRealtimePosition = "M114 R";
    }

    public static GCodeCommand AbsoluteMove(double altDeg, double azDeg) =>
        new($"{Op.AbsoluteMode}\n{Op.RapidMove} X{F(altDeg)} Y{F(azDeg)}", $"Move to alt={altDeg:F4} az={azDeg:F4}");

    public static GCodeCommand TrackedMove(double altDeg, double azDeg, double feedrateDegPerMin) =>
        new($"{Op.LinearMove} X{F(altDeg)} Y{F(azDeg)} F{F(feedrateDegPerMin)}", $"Track to alt={altDeg:F4} az={azDeg:F4} F={feedrateDegPerMin:F4}");

    /// <summary>Query realtime stepper position mid-move (Marlin M114 R).</summary>
    public static GCodeCommand QueryRealtimePosition => new(Op.QueryRealtimePosition, "Query realtime position");

    /// <summary>Quick-stop: cancel all queued moves immediately (Marlin M410).</summary>
    public static GCodeCommand QuickStop => new(Op.QuickStop, "Quick-stop");

    /// <summary>Wait for all moves to complete (Marlin M400). Blocks until planner is empty.</summary>
    public static GCodeCommand WaitForMoves => new(Op.WaitForMoves, "Wait for moves");

    public static GCodeCommand RelativeMove(double dAlt, double dAz) =>
        new($"{Op.RelativeMode}\n{Op.RapidMove} X{F(dAlt)} Y{F(dAz)}\n{Op.AbsoluteMode}", $"Jog dAlt={dAlt:F4} dAz={dAz:F4}");

    public static GCodeCommand HomeX   => new($"{Op.Home} X", "Home altitude axis");
    public static GCodeCommand HomeY   => new($"{Op.Home} Y", "Home azimuth axis");
    public static GCodeCommand HomeAll => new(Op.Home, "Home all axes");

    public static GCodeCommand SetPosition(double altDeg, double azDeg) =>
        new($"{Op.SetPosition} X{F(altDeg)} Y{F(azDeg)}", $"Set position to alt={altDeg:F4} az={azDeg:F4}");

    public static GCodeCommand Raw(string gcode) => new(gcode, "Manual command");

    private static string F(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
}
