using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public class GCodeService : IGCodeService
{
    private readonly ISerialPortService _serial;
    private readonly ILogger<GCodeService> _logger;

    public GCodeService(ISerialPortService serial, ILogger<GCodeService> logger)
    {
        _serial = serial;
        _logger = logger;
    }

    public async Task SendCommandAsync(GCodeCommand command)
    {
        _logger.LogInformation("GCode: {Description}", command.Description);
        var lines = command.Command.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            await _serial.SendLineAsync(line.Trim());
        }
    }

    public async Task<string?> SendRawAsync(string gcode)
    {
        var lines = gcode.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? lastResponse = null;
        foreach (var line in lines)
        {
            lastResponse = await _serial.SendLineAsync(line.Trim());
        }
        return lastResponse;
    }
}
