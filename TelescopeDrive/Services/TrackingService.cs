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

    public void SetTarget(EquatorialCoordinate target, string name)
    {
        State.Target = target;
        State.TargetName = name;
        State.IsSun = false;
        _logger.LogInformation("Target set to {Name}", name);
    }

    public void SetTargetSun()
    {
        State.Target = Catalog.Sun(DateTimeOffset.UtcNow);
        State.TargetName = "Sun";
        State.IsSun = true;
        _logger.LogInformation("Target set to Sun");
    }

    public async Task GotoAsync()
    {
        if (State.Target == null) return;

        var now = DateTimeOffset.UtcNow;
        var target = State.IsSun ? Catalog.Sun(now) : State.Target.Value;
        var horizontal = target.GetHorizontalCoordinate(now, _observer);

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
