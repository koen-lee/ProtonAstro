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
            .Select(h => (Hour: startHour.AddHours(h), H: h))
            .Select(x => (Pos: GetTargetPosition(x.Hour, observer), Label: x.Hour.ToString("HH:mm") + " UTC"))
            .Where(x => x.Pos.Altitude.Degrees >= 0)
            .Select(x => new AltAzDeg(x.Pos.Altitude.Degrees, x.Pos.Azimuth.Degrees, x.Label))
            .ToArray();
    }

    private static readonly (string Name, EquatorialCoordinate Coord)[] OrientationStarData =
    [
        ("Gacrux",  Constellations.SouthernCross[0]),
        ("Acrux",   Constellations.SouthernCross[1]),
        ("Imai",    Constellations.SouthernCross[2]),
        ("Mimosa",  Constellations.SouthernCross[3]),
        ("Dubhe",   Constellations.BigDipper[0]),
        ("Merak",   Constellations.BigDipper[1]),
        ("Phecda",  Constellations.BigDipper[2]),
        ("Megrez",  Constellations.BigDipper[3]),
        ("Alioth",  Constellations.BigDipper[4]),
        ("Mizar",   Constellations.BigDipper[5]),
        ("Alkaid",  Constellations.BigDipper[6]),
    ];

    /// <summary>
    /// Current alt/az positions for Southern Cross and Big Dipper stars, above-horizon only.
    /// </summary>
    public static AltAzDeg[] GetOrientationStars(DateTimeOffset now, WGS84Coordinate observer)
        => OrientationStarData
            .Select(s => (s.Name, Pos: s.Coord.GetHorizontalCoordinate(now, observer)))
            .Where(s => s.Pos.Altitude.Degrees >= 0)
            .Select(s => new AltAzDeg(s.Pos.Altitude.Degrees, s.Pos.Azimuth.Degrees, s.Name))
            .ToArray();
}

/// <summary>Horizontal position for SignalR wire serialization (camelCase: alt, az, label).</summary>
public record AltAzDeg(double Alt, double Az, string Label);
