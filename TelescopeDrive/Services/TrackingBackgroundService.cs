using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TelescopeDrive.Hubs;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

/// <summary>
/// Tracking loop that keeps the telescope pointed at a celestial target.
///
/// Design: the controller owns realtime motion (smooth, jerk-free steps) while
/// the host owns the ephemeris (accurate long-term clock). Each tick we:
///   1. Query the controller's actual stepper position via M114 R (non-blocking, mid-move)
///   2. Compute the tracking error (actual vs where ephemeris+jogOffset says we should be now)
///   3. Compute the target position at now + interval (lookahead)
///   4. Adjust feedrate so the move from actual→future_target takes exactly one interval
///   5. Clamp feedrate to avoid degenerate speeds near zenith (az singularity)
///   6. Send G1 absolute move — goes into planner queue slot ~2, keeps motion smooth
///
/// The target is a Func&lt;DateTimeOffset, EquatorialCoordinate&gt; that abstracts away
/// fixed stars, the Sun, and future ephemeris objects. Jog offsets are accumulated
/// in horizontal (alt/az) space and applied on top, so fine-tuning is preserved.
/// </summary>
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
                if (_tracking.State is { IsTracking: true, TargetFunc: not null })
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
        var observer = _tracking.Observer;
        var intervalMin = intervalMs / 60000.0;

        // Where should we be pointing RIGHT NOW? (ephemeris + jog offset)
        var expectedNow = state.GetTargetPosition(now, observer);

        // Where should we be pointing at the END of the next interval? (lookahead)
        var futureTime = now.AddMilliseconds(intervalMs);
        var expectedFuture = state.GetTargetPosition(futureTime, observer);

        var targetAlt = expectedFuture.Altitude.Degrees;
        var targetAz = expectedFuture.Azimuth.Degrees;

        // Nominal feedrate: angular distance over interval
        var dAlt = targetAlt - expectedNow.Altitude.Degrees;
        var dAz = targetAz - expectedNow.Azimuth.Degrees;
        if (dAz > 180) dAz -= 360;
        if (dAz < -180) dAz += 360;
        var nominalDistance = Math.Sqrt(dAlt * dAlt + dAz * dAz);
        var nominalFeedrate = nominalDistance / intervalMin;

        // Query controller's actual realtime position for error correction
        var actualPos = await _gcode.QueryRealtimePositionAsync();
        double feedrate;

        if (actualPos is var (actualAlt, actualAz))
        {
            // Tracking error: actual position vs where we should be now
            var errAlt = actualAlt - expectedNow.Altitude.Degrees;
            var errAz = actualAz - expectedNow.Azimuth.Degrees;
            if (errAz > 180) errAz -= 360;
            if (errAz < -180) errAz += 360;
            var errorDistance = Math.Sqrt(errAlt * errAlt + errAz * errAz);

            // Adjust feedrate so the move from actual→future_target takes exactly intervalMs
            var moveAlt = targetAlt - actualAlt;
            var moveAz = targetAz - actualAz;
            if (moveAz > 180) moveAz -= 360;
            if (moveAz < -180) moveAz += 360;
            var moveDistance = Math.Sqrt(moveAlt * moveAlt + moveAz * moveAz);
            feedrate = moveDistance / intervalMin;

            _logger.LogDebug(
                "Tracking error: {ErrAlt:F5} alt, {ErrAz:F5} az ({ErrDist:F5} total), feedrate adj: {Nominal:F4} -> {Adjusted:F4} deg/min",
                errAlt, errAz, errorDistance, nominalFeedrate, feedrate);
        }
        else
        {
            feedrate = nominalFeedrate;
        }

        // Clamp feedrate: avoid degenerate speeds near zenith where azimuth rate → infinity
        feedrate = Math.Clamp(feedrate, 0.001, GCodeCommand.MaxFeedrateDegPerMin);

        if (state.LastCommandedPosition is null)
        {
            // First move — slew to current position with rapid move, then start tracking
            await _gcode.SendCommandAsync(GCodeCommand.AbsoluteMove(
                expectedNow.Altitude.Degrees, expectedNow.Azimuth.Degrees));
        }

        // Send the tracked move (absolute G1 — self-correcting, no drift accumulation)
        await _gcode.SendCommandAsync(GCodeCommand.TrackedMove(targetAlt, targetAz, feedrate));

        state.LastCommandedPosition = expectedFuture;
        state.LastUpdateTime = now;

        await _hub.Clients.All.SendAsync("PositionUpdate",
            targetAlt, targetAz, state.TargetName, true);
    }
}
