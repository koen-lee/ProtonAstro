namespace TelescopeDrive.Services;

public record SolveResult(
    double Ra,
    double Dec,
    double FieldRadius,
    double Orientation,
    double PixelScale,
    TimeSpan TimeSpent);

public interface ISolverService
{
    Task<SolveResult?> SolveAsync(string imagePath, CancellationToken ct = default);
}
