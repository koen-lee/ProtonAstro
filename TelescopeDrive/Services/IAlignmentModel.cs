using ProtonAstroLib;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

public interface IAlignmentModel
{
    IReadOnlyList<AlignmentPoint> Points { get; }

    /// <summary>Thread-safe. Called from the SignalR hub thread.</summary>
    void AddPoint(AlignmentPoint point);

    void Clear();

    /// <summary>
    /// IDW-interpolated correction at the given sky position.
    /// Returns (DeltaAlt, DeltaAz) — subtract from desired sky position to get motor command.
    /// Returns (0, 0) when no points are recorded.
    /// </summary>
    (Angle DeltaAlt, Angle DeltaAz) GetCorrection(HorizontalCoordinate horizontal);
}
