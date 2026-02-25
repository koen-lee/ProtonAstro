using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public interface IGCodeService
{
    Task SendCommandAsync(GCodeCommand command);
    Task<string?> SendRawAsync(string gcode);

    /// <summary>
    /// Queries the controller's realtime stepper position via M114 R (non-blocking, mid-move).
    /// Returns (altDeg, azDeg) or null if the response could not be parsed.
    /// </summary>
    Task<(double alt, double az)?> QueryRealtimePositionAsync();
}
