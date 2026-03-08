using ProtonAstroLib;

namespace TelescopeDrive.Models;

public class TrackingState
{
    /// <summary>
    /// Returns the equatorial coordinate for the target at any given moment.
    /// Abstracts away fixed catalog stars (constant), the Sun (time-dependent),
    /// and future ephemeris targets like planets or comets.
    /// After "Adopt Position", this func is replaced with one that tracks the
    /// jogged sky position in equatorial coordinates.
    /// </summary>
    public Func<DateTimeOffset, EquatorialCoordinate>? TargetFunc { get; set; }

    public string? TargetName { get; set; }
    public bool IsTracking { get; set; }
    public HorizontalCoordinate? LastCommandedPosition { get; set; }
    public DateTimeOffset? LastUpdateTime { get; set; }

    /// <summary>
    /// Active jog offset in horizontal (alt/az) degrees, accumulated from fine-tuning.
    /// Zeroed when "Adopt Position" bakes it into a new equatorial target via the
    /// horizontal→equatorial inverse transform.
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

    /// <summary>
    /// Whole-hour alt/az positions for the tracked target over [-12h, +12h] from now,
    /// filtered to above-horizon only. Empty when not tracking.
    /// </summary>
    public AltAzDeg[] GetTrackingList(DateTimeOffset now, WGS84Coordinate observer)
    {
        var startHour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
        return Enumerable.Range(-12, 25)
            .Select(h => startHour.AddHours(h))
            .Select(t => GetTargetPosition(t, observer))
            .Where(p => p.Altitude.Degrees >= 0)
            .Select(p => new AltAzDeg(p.Altitude.Degrees, p.Azimuth.Degrees))
            .ToArray();
    }

    /// <summary>
    /// Current alt/az positions for Southern Cross and Big Dipper stars, above-horizon only.
    /// </summary>
    public static AltAzDeg[] GetOrientationStars(DateTimeOffset now, WGS84Coordinate observer)
        => Constellations.SouthernCross
            .Concat(Constellations.BigDipper)
            .Select(c => c.GetHorizontalCoordinate(now, observer))
            .Where(p => p.Altitude.Degrees >= 0)
            .Select(p => new AltAzDeg(p.Altitude.Degrees, p.Azimuth.Degrees))
            .ToArray();
}

/// <summary>Horizontal position for SignalR wire serialization (camelCase: alt, az).</summary>
public record AltAzDeg(double Alt, double Az);
