using WatneyAstrometry.Core;
using WatneyAstrometry.Core.QuadDb;
using WatneyAstrometry.Core.Types;
using WatneyAstrometry.ImageReaders;

namespace TelescopeDrive.Services;

public class WatneySolverService(IConfiguration config) : ISolverService
{
    public async Task<SolveResult?> SolveAsync(string imagePath, SolveHint? hint = null, CancellationToken ct = default)
    {
        var dbPath = config["PlateSolver:QuadDatabasePath"];
        if (string.IsNullOrWhiteSpace(dbPath)) return null;

        var quadDb = new CompactQuadDatabase().UseDataSource(dbPath);
        var solver = new Solver()
            .UseQuadDatabase(quadDb)
            .UseImageReader<CommonFormatsImageReader>(() => new CommonFormatsImageReader(), "jpg", "jpeg", "png");

        ISearchStrategy strategy = hint != null
            ? new NearbySearchStrategy(new NearbySearchStrategyOptions
            {
                SearchOrigin = new EquatorialCoords(hint.RaDeg, hint.DecDeg),
                SearchRadius = hint.SearchRadiusDeg,
                UseParallelism = true,
                MaxNegativeDensityOffset = 2,
                MaxPositiveDensityOffset = 2
            })
            : new BlindSearchStrategy(new BlindSearchStrategyOptions
            {
                UseParallelism = true,
                MaxNegativeDensityOffset = 2,
                MaxPositiveDensityOffset = 2
            });

        var result = await solver.SolveFieldAsync(imagePath, strategy, new SolverOptions(), ct);
        if (!result.Success) return null;

        return new SolveResult(
            result.Solution.PlateCenter.Ra,
            result.Solution.PlateCenter.Dec,
            result.Solution.Radius,
            result.Solution.Orientation,
            result.Solution.PixelScale,
            result.TimeSpent);
    }
}
