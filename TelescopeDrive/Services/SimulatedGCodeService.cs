using System.Globalization;
using System.Text.RegularExpressions;
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
        string? lastResponse = null;
        foreach (var line in gcode.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            ApplyLine(trimmed);
            lastResponse = BuildResponse(trimmed);
        }
        return Task.FromResult(lastResponse);
    }

    public Task<(double alt, double az)?> QueryRealtimePositionAsync()
    {
        _logger.LogDebug("SIM M114 R → alt={Alt:F4} az={Az:F4}", _alt, _az);
        return Task.FromResult<(double, double)?>( (_alt, _az) );
    }

    // -------------------------------------------------------------------------

    private void ApplyLine(string line)
    {
        var upper = line.ToUpperInvariant();

        if (upper == GCodeCommand.Op.AbsoluteMode) { _relativeMode = false; return; }
        if (upper == GCodeCommand.Op.RelativeMode) { _relativeMode = true;  return; }
        if (upper == GCodeCommand.Op.QuickStop || upper == GCodeCommand.Op.WaitForMoves) return;

        // G28 / G28 X / G28 Y — home
        if (upper.StartsWith(GCodeCommand.Op.Home))
        {
            var rest = upper[GCodeCommand.Op.Home.Length..].Trim();
            if (rest == "" || rest.StartsWith('X')) _alt = 0;
            if (rest == "" || rest.StartsWith('Y')) _az  = 0;
            _relativeMode = false;
            _logger.LogDebug("SIM home → alt={Alt:F4} az={Az:F4}", _alt, _az);
            return;
        }

        // G92 X... Y... — set position
        if (upper.StartsWith(GCodeCommand.Op.SetPosition))
        {
            var x = ParseParam(upper, 'X');
            var y = ParseParam(upper, 'Y');
            if (x.HasValue) _alt = x.Value;
            if (y.HasValue) _az  = y.Value;
            _logger.LogDebug("SIM G92 → alt={Alt:F4} az={Az:F4}", _alt, _az);
            return;
        }

        // G0 / G1 — move
        if (upper.StartsWith(GCodeCommand.Op.RapidMove) || upper.StartsWith(GCodeCommand.Op.LinearMove))
        {
            var x = ParseParam(upper, 'X');
            var y = ParseParam(upper, 'Y');
            if (_relativeMode)
            {
                if (x.HasValue) _alt += x.Value;
                if (y.HasValue) _az  += y.Value;
            }
            else
            {
                if (x.HasValue) _alt = x.Value;
                if (y.HasValue) _az  = y.Value;
            }
            _logger.LogDebug("SIM move → alt={Alt:F4} az={Az:F4}", _alt, _az);
        }
    }

    private string BuildResponse(string line)
    {
        if (line.StartsWith(GCodeCommand.Op.QueryPosition, StringComparison.OrdinalIgnoreCase))
            return $"X:{_alt.ToString("F4", CultureInfo.InvariantCulture)} Y:{_az.ToString("F4", CultureInfo.InvariantCulture)} Z:0.0000 E:0.0000 Count X:0 Y:0 Z:0";
        return "ok";
    }

    private static double? ParseParam(string line, char param)
    {
        var match = Regex.Match(line, $@"{param}:?(-?[\d.]+)", RegexOptions.IgnoreCase);
        return match.Success ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }
}
