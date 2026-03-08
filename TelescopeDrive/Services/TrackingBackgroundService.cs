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
///   2. Compute the tracking error (actual vs ephemeris+offset target)
///   3. Compute the target position at now + interval (lookahead)
///   4. Adjust feedrate so the move from actual→future_target takes exactly one interval
///   5. Clamp feedrate to avoid degenerate speeds near zenith (az singularity)
///   6. Send G1 absolute move — goes into planner queue slot ~2, keeps motion smooth
///
/// The tick delay uses a linked CancellationToken: the host stoppingToken AND the
/// tracking service's TickInterruptToken. When a jog interrupts, the delay is
/// cancelled immediately so the loop can restart from the new position.
/// </summary>
public class TrackingBackgroundService : BackgroundService
{
    private readonly ITrackingService _tracking;
    private readonly IGCodeService _gcode;
    private readonly IHubContext<TelescopeHub> _hub;
    private readonly IAlignmentModel _alignment;
    private readonly IOptions<ObserverConfig> _config;
    private readonly ILogger<TrackingBackgroundService> _logger;

    public TrackingBackgroundService(
        ITrackingService tracking,
        IGCodeService gcode,
        IHubContext<TelescopeHub> hub,
        IAlignmentModel alignment,
        IOptions<ObserverConfig> config,
        ILogger<TrackingBackgroundService> logger)
    {
        _tracking = tracking;
        _gcode = gcode;
        _hub = hub;
        _alignment = alignment;
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
                    await TickAsync(intervalMs, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // Tick was interrupted by a jog — this is expected, loop restarts
                _logger.LogDebug("Tracking tick interrupted by jog");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tracking loop");
            }

            // Wait for the next tick, but allow jog interrupts to wake us early
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken, _tracking.TickInterruptToken);
                await Task.Delay(intervalMs, linked.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // Interrupted by jog — skip delay, loop restarts immediately
            }
        }
    }

    private async Task TickAsync(int intervalMs, CancellationToken stoppingToken)
    {
        // Link to both the host stopping token and the jog interrupt token
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken, _tracking.TickInterruptToken);
        var ct = linked.Token;

        var state = _tracking.State;
        var now = DateTimeOffset.UtcNow;
        var observer = _tracking.Observer;
        // add a small buffer to ensure the move does not complete before the next tick
        // we don't want the controller to be idle at all, or it might start decelerating and cause jerkiness
        var moveInterval = TimeSpan.FromMilliseconds(intervalMs + 500);
        
        // Where should we be pointing RIGHT NOW? (ephemeris + total offset)
        var expectedNow = state.GetTargetPosition(now, observer);

        // Where should we be pointing at the END of the next interval? (lookahead)
        var futureTime = now.AddMilliseconds(intervalMs + 500);
        var expectedFuture = state.GetTargetPosition(futureTime, observer);

        // Apply alignment correction to get motor-space targets.
        // Nominal feedrate uses sky-space delta (how fast the celestial target moves).
        var (corrNowAlt, corrNowAz) = _alignment.GetCorrection(expectedNow);
        var (corrFutAlt, corrFutAz) = _alignment.GetCorrection(expectedFuture);

        var motorNowAlt = expectedNow.Altitude.Degrees - corrNowAlt.Degrees;
        var motorNowAz  = expectedNow.Azimuth.Degrees  - corrNowAz.Degrees;
        var targetAlt   = expectedFuture.Altitude.Degrees - corrFutAlt.Degrees;
        var targetAz    = expectedFuture.Azimuth.Degrees  - corrFutAz.Degrees;

        ct.ThrowIfCancellationRequested();

        // Nominal feedrate: angular distance in sky-space over interval
        var dAlt = expectedFuture.Altitude.Degrees - expectedNow.Altitude.Degrees;
        var dAz = expectedFuture.Azimuth.Degrees - expectedNow.Azimuth.Degrees;
        if (dAz > 180) dAz -= 360;
        if (dAz < -180) dAz += 360;
        var nominalDistance = Math.Sqrt(dAlt * dAlt + dAz * dAz);
        // feedrate is in degrees per minute, so convert interval from ms to min
        var nominalFeedrate = nominalDistance / moveInterval.TotalMinutes;

        // Query controller's actual realtime position for error correction.
        // actualAlt/Az are motor-space; compare against motor-space targets.
        var actualPos = await _gcode.QueryRealtimePositionAsync();
        double feedrate;

        ct.ThrowIfCancellationRequested();

        if (actualPos is var (actualAltAngle, actualAzAngle))
        {
            var actualAlt = actualAltAngle.Degrees;
            var actualAz  = actualAzAngle.Degrees;
            var errAlt = actualAlt - motorNowAlt;
            var errAz = actualAz - motorNowAz;
            if (errAz > 180) errAz -= 360;
            if (errAz < -180) errAz += 360;
            var errorDistance = Math.Sqrt(errAlt * errAlt + errAz * errAz);

            // Adjust feedrate so the move from actual→future_target takes exactly moveInterval
            var moveAlt = targetAlt - actualAlt;
            var moveAz = targetAz - actualAz;
            if (moveAz > 180) moveAz -= 360;
            if (moveAz < -180) moveAz += 360;
            var moveDistance = Math.Sqrt(moveAlt * moveAlt + moveAz * moveAz);
            feedrate = moveDistance / moveInterval.TotalMinutes;

            _logger.LogDebug(
                "Tracking error: {ErrAlt:F5} alt, {ErrAz:F5} az ({ErrDist:F5} total), feedrate adj: {Nominal:F4} -> {Adjusted:F4} deg/min",
                errAlt, errAz, errorDistance, nominalFeedrate, feedrate);
        }
        else
        {
            feedrate = nominalFeedrate;
        }

        feedrate = Math.Clamp(feedrate, 0.001, _config.Value.MaxFeedrateDegPerMin);

        ct.ThrowIfCancellationRequested();

        if (state.LastCommandedPosition is null)
        {
            await _gcode.SendCommandAsync(GCodeCommand.AbsoluteMove(motorNowAlt, motorNowAz));
        }

        await _gcode.SendCommandAsync(GCodeCommand.TrackedMove(targetAlt, targetAz, feedrate));

        state.LastCommandedPosition = expectedFuture;
        state.LastUpdateTime = now;

        var eq = state.TargetFunc!(now);
        await _hub.Clients.All.SendAsync("PositionUpdate",
            expectedFuture.Altitude.Degrees, expectedFuture.Azimuth.Degrees,
            state.TargetName, true,
            eq.RightAscension.Degrees, eq.Declination.Degrees, ct);
    }
}
