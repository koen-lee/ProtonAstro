using ProtonAstroLib;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public interface ITrackingService
{
    TrackingState State { get; }
    WGS84Coordinate Observer { get; }
    void SetObserverLocation(double latDeg, double lonDeg);
    void SetTarget(EquatorialCoordinate target, string name);
    void SetTargetSun();
    Task GotoAsync();
    void StartTracking();
    void StopTracking();
}
