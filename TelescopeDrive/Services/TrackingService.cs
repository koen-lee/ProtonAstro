using Microsoft.Extensions.Options;
using ProtonAstroLib;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public class TrackingService : ITrackingService
{
    private readonly IGCodeService _gcode;
    private readonly ILogger<TrackingService> _logger;
    private WGS84Coordinate _observer;

    public TrackingService(IGCodeService gcode, IOptions<ObserverConfig> config, ILogger<TrackingService> logger)
    {
        _gcode = gcode;
        _logger = logger;
        _observer = config.Value.ToWGS84();
    }

    public TrackingState State { get; } = new();
    public WGS84Coordinate Observer => _observer;

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

    public void Jog(double dAltDeg, double dAzDeg)
    {
        State.JogOffsetAltDeg += dAltDeg;
        State.JogOffsetAzDeg += dAzDeg;
        _logger.LogInformation("Jog offset now: alt={Alt:F4} az={Az:F4}",
            State.JogOffsetAltDeg, State.JogOffsetAzDeg);
    }

    public async Task GotoAsync()
    {
        if (State.TargetFunc == null) return;

        var now = DateTimeOffset.UtcNow;
        var horizontal = State.GetTargetPosition(now, _observer);

        await _gcode.SendCommandAsync(GCodeCommand.AbsoluteMove(
            horizontal.Altitude.Degrees, horizontal.Azimuth.Degrees));

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
