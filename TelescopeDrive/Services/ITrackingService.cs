using ProtonAstroLib;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public interface ITrackingService
{
    TrackingState State { get; }
    WGS84Coordinate Observer { get; }

    /// <summary>
    /// Cancellation token that the tracking loop should observe.
    /// Cancelled when a jog interrupts the tick cycle, then renewed.
    /// </summary>
    CancellationToken TickInterruptToken { get; }

    void SetObserverLocation(double latDeg, double lonDeg);
    void SetTarget(Func<DateTimeOffset, EquatorialCoordinate> targetFunc, string name);

    /// <summary>
    /// Jog: interrupts the tracking loop (M410 quick-stop), sends the relative move,
    /// waits for it to complete (M400), then lets the tracking loop restart from the
    /// new position. Accumulates the offset so tracking preserves it.
    /// </summary>
    Task JogAsync(double dAltDeg, double dAzDeg);

    /// <summary>
    /// Zero the jog offset by converting the current offset position to an equivalent
    /// fixed equatorial target, so tracking continues from exactly where we are now.
    /// </summary>
    void AdoptPosition();

    Task GotoAsync();
    void StartTracking();
    void StopTracking();
}
