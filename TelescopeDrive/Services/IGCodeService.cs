using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public interface IGCodeService
{
    Task SendCommandAsync(GCodeCommand command);
    Task<string?> SendRawAsync(string gcode);
}
