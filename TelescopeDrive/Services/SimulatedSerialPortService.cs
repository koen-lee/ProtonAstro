using System.Globalization;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

/// <summary>
/// Simulates a Marlin-firmware serial device. Implements ISerialPortService so that
/// GCodeService works unchanged against both real hardware and this simulator.
/// </summary>
public class SimulatedSerialPortService : ISerialPortService
{
    private readonly ILogger<SimulatedSerialPortService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private double _alt = 0.0;
    private double _az = 0.0;
    private bool _relativeMode = false;

    public bool IsConnected => true;
    public bool IsSimulated => true;
    public string? CurrentPort => "simulated";
    public IReadOnlyList<string> AvailablePorts => ["simulated"];

    public event Action<string>? LineSent;
    public event Action<string>? LineReceived;

    public SimulatedSerialPortService(ILogger<SimulatedSerialPortService> logger)
    {
        _logger = logger;
        _logger.LogInformation("SimulatedSerialPortService active — no serial port required.");
    }

    public Task ConnectAsync(string portName, int baudRate) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;

    public async Task<string?> SendLineAsync(string command)
    {
        await _semaphore.WaitAsync();
        try
        {
            LineSent?.Invoke(command);
            _logger.LogDebug("SIM TX: {Command}", command);

            var response = await ApplyLine(command.Trim());

            LineReceived?.Invoke(response);
            _logger.LogDebug("SIM RX: {Response}", response);
            return response;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string> ApplyLine(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return "ok";

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
                break;

            case GCodeCommand.Op.WaitForMoves:
                await Task.Delay(500);
                break;

            case GCodeCommand.Op.Home:
                var axes = string.Concat(tokens[1..]).ToUpperInvariant();
                if (axes == "" || axes.Contains('X')) _alt = 0;
                if (axes == "" || axes.Contains('Y')) _az = 0;
                _relativeMode = false;
                _logger.LogDebug("SIM home → alt={Alt:F4} az={Az:F4}", _alt, _az);
                break;

            case GCodeCommand.Op.SetPosition:
                if (TryParseParam(tokens, 'X', out var sx)) _alt = sx;
                if (TryParseParam(tokens, 'Y', out var sy)) _az = sy;
                _logger.LogDebug("SIM G92 → alt={Alt:F4} az={Az:F4}", _alt, _az);
                break;

            case GCodeCommand.Op.RapidMove:
            case GCodeCommand.Op.LinearMove:
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

            case "M114": // handles both M114 and M114 R
                return $"X:{_alt.ToString("F4", CultureInfo.InvariantCulture)} Y:{_az.ToString("F4", CultureInfo.InvariantCulture)} Z:0.0000 E:0.0000 Count X:0 Y:0 Z:0";
        }

        return "ok";
    }

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
