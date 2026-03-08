namespace TelescopeDrive.Services;

public record SolveResult(
    double Ra,
    double Dec,
    double FieldRadius,
    double Orientation,
    double PixelScale,
    TimeSpan TimeSpent);

/// <summary>
/// Hint for a nearby plate solve. When provided the solver searches within
/// <see cref="SearchRadiusDeg"/> of the expected pointing, which is much faster
/// than a full blind search.
/// </summary>
public record SolveHint(double RaDeg, double DecDeg, double SearchRadiusDeg = 15);

public interface ISolverService
{
    Task<SolveResult?> SolveAsync(string imagePath, SolveHint? hint = null, CancellationToken ct = default);
}
