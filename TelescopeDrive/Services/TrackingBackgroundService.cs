using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ProtonAstroLib;
using TelescopeDrive.Hubs;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public class TrackingBackgroundService : BackgroundService
{
    private readonly ITrackingService _tracking;
    private readonly IGCodeService _gcode;
    private readonly IHubContext<TelescopeHub> _hub;
    private readonly IOptions<ObserverConfig> _config;
    private readonly ILogger<TrackingBackgroundService> _logger;

    public TrackingBackgroundService(
        ITrackingService tracking,
        IGCodeService gcode,
        IHubContext<TelescopeHub> hub,
        IOptions<ObserverConfig> config,
        ILogger<TrackingBackgroundService> logger)
    {
        _tracking = tracking;
        _gcode = gcode;
        _hub = hub;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tracking background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalMs = _config.Value.TrackingIntervalMs;

            try
            {
                if (_tracking.State is { IsTracking: true, Target: not null })
                {
                    await TickAsync(intervalMs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tracking loop");
            }

            await Task.Delay(intervalMs, stoppingToken);
        }
    }

    private async Task TickAsync(int intervalMs)
    {
        var state = _tracking.State;
        var now = DateTimeOffset.UtcNow;
        var futureTime = now.AddMilliseconds(intervalMs);
        var observer = _tracking.Observer;

        // Get target coordinates at the future time
        var target = state.IsSun ? Catalog.Sun(futureTime) : state.Target!.Value;
        var futureHorizontal = target.GetHorizontalCoordinate(futureTime, observer);

        var altDeg = futureHorizontal.Altitude.Degrees;
        var azDeg = futureHorizontal.Azimuth.Degrees;

        // Calculate feedrate: degrees of travel / interval converted to deg/min
        if (state.LastCommandedPosition is { } lastPos)
        {
            var distanceDeg = futureHorizontal.Distance(lastPos).Degrees;
            var intervalMin = intervalMs / 60000.0;
            var feedrate = distanceDeg / intervalMin;

            if (distanceDeg > 0.0005) // dead-band
            {
                await _gcode.SendCommandAsync(
                    GCodeCommand.TrackedMove(altDeg, azDeg, feedrate));
            }
        }
        else
        {
            // First move after starting tracking - use absolute goto
            await _gcode.SendCommandAsync(GCodeCommand.AbsoluteMove(altDeg, azDeg));
        }

        state.LastCommandedPosition = futureHorizontal;
        state.LastUpdateTime = now;

        // Push position to all clients
        await _hub.Clients.All.SendAsync("PositionUpdate",
            altDeg, azDeg, state.TargetName, true);
    }
}
