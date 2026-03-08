using System.Globalization;
using System.Text.RegularExpressions;
using ProtonAstroLib;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public partial class GCodeService : IGCodeService
{
    private readonly ISerialPortService _serial;
    private readonly ILogger<GCodeService> _logger;
    // Command-level lock: ensures multi-line commands (AbsoluteMove, RelativeMove) are sent
    // atomically and that hub queries cannot interleave with tracking-loop command sequences.
    private readonly SemaphoreSlim _commandLock = new(1, 1);

    public GCodeService(ISerialPortService serial, ILogger<GCodeService> logger)
    {
        _serial = serial;
        _logger = logger;
    }

    public async Task SendCommandAsync(GCodeCommand command)
    {
        await _commandLock.WaitAsync();
        try
        {
            LogCommand(command.Description ?? command.Command);
            var lines = command.Command.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                await _serial.SendLineAsync(line.Trim());
            }
        }
        finally
        {
            _commandLock.Release();
        }
    }

    public async Task<string?> SendRawAsync(string gcode)
    {
        await _commandLock.WaitAsync();
        try
        {
            var lines = gcode.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? lastResponse = null;
            foreach (var line in lines)
            {
                lastResponse = await _serial.SendLineAsync(line.Trim());
            }
            return lastResponse;
        }
        finally
        {
            _commandLock.Release();
        }
    }

    public async Task<(Angle alt, Angle az)?> QueryRealtimePositionAsync()
    {
        await _commandLock.WaitAsync();
        try
        {
            var response = await _serial.SendLineAsync("M114 R");
            if (response == null) return null;

            // Marlin M114 R response format: "X:12.3456 Y:34.5678 Z:0.0000 E:0.0000 Count X:..."
            var match = M114Pattern().Match(response);
            if (!match.Success)
            {
                _logger.LogWarning("Could not parse M114 R response: {Response}", response);
                return null;
            }

            var x = double.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture);
            var y = double.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture);
            return (Angle.FromDegrees(x), Angle.FromDegrees(y));
        }
        finally
        {
            _commandLock.Release();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "GCode: {Description}")]
    private partial void LogCommand(string description);

    [GeneratedRegex(@"X:(?<x>-?[\d.]+)\s+Y:(?<y>-?[\d.]+)")]
    private static partial Regex M114Pattern();
}
