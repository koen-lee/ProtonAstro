using ProtonAstroLib;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public interface ITrackingService
{
    TrackingState State { get; }
    WGS84Coordinate Observer { get; }
    void SetObserverLocation(double latDeg, double lonDeg);
    void SetTarget(Func<DateTimeOffset, EquatorialCoordinate> targetFunc, string name);
    void Jog(double dAltDeg, double dAzDeg);
    Task GotoAsync();
    void StartTracking();
    void StopTracking();
}
