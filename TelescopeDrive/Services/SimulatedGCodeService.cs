using System.Globalization;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

/// <summary>
/// In-process simulation of IGCodeService used when SerialPort is set to "simulated".
/// Parses GCode commands and maintains an in-memory alt/az position.
/// </summary>
public partial class SimulatedGCodeService : IGCodeService
{
    private readonly ILogger<SimulatedGCodeService> _logger;

    private double _alt = 0.0;
    private double _az = 0.0;
    private bool _relativeMode = false;

    public SimulatedGCodeService(ILogger<SimulatedGCodeService> logger)
    {
        _logger = logger;
        _logger.LogInformation("SimulatedGCodeService active — no serial port required.");
    }

    public Task SendCommandAsync(GCodeCommand command)
    {
        _logger.LogInformation("SIM GCode: {Description}", command.Description);
        var lines = command.Command.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
            ApplyLine(line.Trim());
        return Task.CompletedTask;
    }

    public Task<string?> SendRawAsync(string gcode)
    {
        Task<string> result = Task.FromResult("ok");
        foreach (var line in gcode.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            result = result.ContinueWith(_ => ApplyLine(trimmed)).Unwrap();
        }
        return result;
    }

    public Task<(double alt, double az)?> QueryRealtimePositionAsync()
    {
        _logger.LogDebug("SIM M114 R → alt={Alt:F4} az={Az:F4}", _alt, _az);
        return Task.FromResult<(double, double)?>((_alt, _az));
    }

    // -------------------------------------------------------------------------

    private Task<string> ApplyLine(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return Task.FromResult("ok");

        var opcode = tokens[0].ToUpperInvariant();

        switch (opcode)
        {
            case GCodeCommand.Op.AbsoluteMode:
                _relativeMode = false;
                break;

            case GCodeCommand.Op.RelativeMode:
                _relativeMode = true;
                break;

            case GCodeCommand.Op.QuickStop:
                _logger.LogDebug("SIM quick-stop");
                break; // no-op
            case GCodeCommand.Op.WaitForMoves:
                _logger.LogDebug("SIM wait-for-moves");
                return Task.Delay(500).ContinueWith(_ => "ok");

            case GCodeCommand.Op.Home: // G28, G28 X, G28 Y
                var axes = string.Concat(tokens[1..]).ToUpperInvariant();
                if (axes == "" || axes.Contains('X')) _alt = 0;
                if (axes == "" || axes.Contains('Y')) _az = 0;
                _relativeMode = false;
                _logger.LogDebug("SIM home → alt={Alt:F4} az={Az:F4}", _alt, _az);
                break;

            case GCodeCommand.Op.SetPosition: // G92 X... Y...
                if (TryParseParam(tokens, 'X', out var sx)) _alt = sx;
                if (TryParseParam(tokens, 'Y', out var sy)) _az = sy;
                _logger.LogDebug("SIM G92 → alt={Alt:F4} az={Az:F4}", _alt, _az);
                break;

            case GCodeCommand.Op.RapidMove:  // G0
            case GCodeCommand.Op.LinearMove: // G1
                if (_relativeMode)
                {
                    if (TryParseParam(tokens, 'X', out var rx)) _alt += rx;
                    if (TryParseParam(tokens, 'Y', out var ry)) _az += ry;
                }
                else
                {
                    if (TryParseParam(tokens, 'X', out var ax)) _alt = ax;
                    if (TryParseParam(tokens, 'Y', out var ay)) _az = ay;
                }
                _logger.LogDebug("SIM move → alt={Alt:F4} az={Az:F4}", _alt, _az);
                break;
            case GCodeCommand.Op.QueryRealtimePosition:
                return Task.FromResult($"X:{_alt.ToString("F4", CultureInfo.InvariantCulture)} Y:{_az.ToString("F4", CultureInfo.InvariantCulture)} Z:0.0000 E:0.0000 Count X:0 Y:0 Z:0");
        }
        return Task.FromResult("ok");
    }

    // Finds a token like "X12.3456" or "X-1.0" among the already-split operand tokens (index 1+).
    private static bool TryParseParam(string[] tokens, char param, out double value)
    {
        foreach (var token in tokens.AsSpan(1))
        {
            if (token.Length > 1 &&
                char.ToUpperInvariant(token[0]) == char.ToUpperInvariant(param) &&
                double.TryParse(token.AsSpan(1), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
        }
        value = 0;
        return false;
    }
}
