using ProtonAstroLib;

namespace TelescopeDrive.Models;

public class TrackingState
{
    public EquatorialCoordinate? Target { get; set; }
    public string? TargetName { get; set; }
    public bool IsSun { get; set; }
    public bool IsTracking { get; set; }
    public HorizontalCoordinate? LastCommandedPosition { get; set; }
    public DateTimeOffset? LastUpdateTime { get; set; }
}
