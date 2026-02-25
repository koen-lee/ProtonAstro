using ProtonAstroLib;

namespace TelescopeDrive.Models;

public class TrackingState
{
    /// <summary>
    /// Returns the equatorial coordinate for the target at any given moment.
    /// Abstracts away fixed catalog stars (constant), the Sun (time-dependent),
    /// and future ephemeris targets like planets or comets.
    /// </summary>
    public Func<DateTimeOffset, EquatorialCoordinate>? TargetFunc { get; set; }

    public string? TargetName { get; set; }
    public bool IsTracking { get; set; }
    public HorizontalCoordinate? LastCommandedPosition { get; set; }
    public DateTimeOffset? LastUpdateTime { get; set; }

    /// <summary>
    /// Accumulated jog offset in horizontal (alt/az) degrees.
    /// Applied on top of the ephemeris position so fine-tuning is preserved during tracking.
    /// Use cases: non-level mount compensation, exploring within a nebula, manual offset
    /// for uncharted objects.
    /// </summary>
    public double JogOffsetAltDeg { get; set; }
    public double JogOffsetAzDeg { get; set; }

    /// <summary>
    /// Compute the effective horizontal position at a given moment: ephemeris + jog offset.
    /// </summary>
    public HorizontalCoordinate GetTargetPosition(DateTimeOffset moment, WGS84Coordinate observer)
    {
        var equatorial = TargetFunc!(moment);
        var horizontal = equatorial.GetHorizontalCoordinate(moment, observer);
        return new HorizontalCoordinate(
            horizontal.Altitude + Angle.FromDegrees(JogOffsetAltDeg),
            horizontal.Azimuth + Angle.FromDegrees(JogOffsetAzDeg));
    }
}
