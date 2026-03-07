using Microsoft.Extensions.Options;
using ProtonAstroLib;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public class TrackingService : ITrackingService
{
    private readonly IGCodeService _gcode;
    private readonly IAlignmentModel _alignment;
    private readonly ILogger<TrackingService> _logger;
    private WGS84Coordinate _observer;
    private CancellationTokenSource _tickInterruptCts = new();

    public TrackingService(IGCodeService gcode, IAlignmentModel alignment, IOptions<ObserverConfig> config, ILogger<TrackingService> logger)
    {
        _gcode = gcode;
        _alignment = alignment;
        _logger = logger;
        _observer = config.Value.ToWGS84();
    }

    public TrackingState State { get; } = new();
    public WGS84Coordinate Observer => _observer;
    public CancellationToken TickInterruptToken => _tickInterruptCts.Token;

    public void SetObserverLocation(double latDeg, double lonDeg)
    {
        _observer = new WGS84Coordinate(Angle.FromDegrees(latDeg), Angle.FromDegrees(lonDeg));
        _logger.LogInformation("Observer location set to {Lat}, {Lon}", latDeg, lonDeg);
    }

    public void SetTarget(Func<DateTimeOffset, EquatorialCoordinate> targetFunc, string name)
    {
        State.TargetFunc = targetFunc;
        State.TargetName = name;
        State.JogOffsetAltDeg = 0;
        State.JogOffsetAzDeg = 0;
        _logger.LogInformation("Target set to {Name}", name);
    }

    public async Task JogAsync(double dAltDeg, double dAzDeg)
    {
        // Interrupt the current tracking tick so it doesn't fight the jog
        InterruptTick();

        // Quick-stop any queued tracking moves
        await _gcode.SendCommandAsync(GCodeCommand.QuickStop);

        // Accumulate active jog offset (tracking loop will pick this up)
        State.JogOffsetAltDeg += dAltDeg;
        State.JogOffsetAzDeg += dAzDeg;
        _logger.LogInformation("Jog offset: alt={Alt:F4} az={Az:F4}",
            State.JogOffsetAltDeg, State.JogOffsetAzDeg);

        // Send the relative move to the controller
        await _gcode.SendCommandAsync(GCodeCommand.RelativeMove(dAltDeg, dAzDeg));

        // Wait for the jog move to physically complete (can be slow with gear reduction)
        await _gcode.SendCommandAsync(GCodeCommand.WaitForMoves);

        // Clear last commanded position so tracking restarts cleanly from M114 R feedback
        State.LastCommandedPosition = null;
    }

    public void AdoptPosition()
    {
        if (State.TargetFunc == null) return;
        if (State.JogOffsetAltDeg == 0 && State.JogOffsetAzDeg == 0) return;

        var now = DateTimeOffset.UtcNow;
        var observer = _observer;

        // Get the current effective horizontal position (ephemeris + jog offset)
        var offsetHorizontal = State.GetTargetPosition(now, observer);

        // Inverse transform: horizontal → equatorial at this moment
        // This gives us the RA/Dec of the sky point we're actually looking at
        var adoptedEquatorial = offsetHorizontal.ToEquatorialCoordinate(now, observer);

        _logger.LogInformation(
            "Adopting position: jog alt={JogAlt:F4} az={JogAz:F4} → RA={RA} Dec={Dec}",
            State.JogOffsetAltDeg, State.JogOffsetAzDeg,
            adoptedEquatorial.RightAscension.ToString("HMS", null),
            adoptedEquatorial.Declination.ToDMSString());

        // Replace the target with the adopted equatorial coordinate.
        // This is now a fixed sky point (equinox-of-date) that will be properly
        // tracked as the Earth rotates — the jog has become a celestial position.
        var adopted = adoptedEquatorial;
        State.TargetFunc = _ => adopted;
        State.TargetName = $"{State.TargetName} (adopted)";
        State.JogOffsetAltDeg = 0;
        State.JogOffsetAzDeg = 0;
    }

    private void InterruptTick()
    {
        _tickInterruptCts.Cancel();
        _tickInterruptCts.Dispose();
        _tickInterruptCts = new CancellationTokenSource();
    }

    public async Task GotoAsync()
    {
        if (State.TargetFunc == null) return;

        var now = DateTimeOffset.UtcNow;
        var horizontal = State.GetTargetPosition(now, _observer);

        var (corrAlt, corrAz) = _alignment.GetCorrection(
            horizontal.Altitude.Degrees, horizontal.Azimuth.Degrees);

        await _gcode.SendCommandAsync(GCodeCommand.AbsoluteMove(
            horizontal.Altitude.Degrees - corrAlt,
            horizontal.Azimuth.Degrees  - corrAz));

        // Store sky-space position so the tracking loop error comparison stays consistent.
        State.LastCommandedPosition = horizontal;
        State.LastUpdateTime = now;
    }

    public void StartTracking()
    {
        State.IsTracking = true;
        _logger.LogInformation("Tracking started for {Name}", State.TargetName);
    }

    public void StopTracking()
    {
        State.IsTracking = false;
        _logger.LogInformation("Tracking stopped");
    }
}
