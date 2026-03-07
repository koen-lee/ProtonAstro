using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WatneyAstrometry.Core;
using WatneyAstrometry.Core.QuadDb;
using WatneyAstrometry.Core.Types;
using WatneyAstrometry.ImageReaders;

namespace TelescopeDrive.Pages;

[IgnoreAntiforgeryToken]
public class SolverCalibrationModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly ILogger<SolverCalibrationModel> _logger;

    public SolverCalibrationModel(IConfiguration config, ILogger<SolverCalibrationModel> logger)
    {
        _config = config;
        _logger = logger;
    }

    [RequestSizeLimit(100 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
    public async Task<IActionResult> OnPostSolveAsync(IFormFile image)
    {
        if (image == null || image.Length == 0)
            return new JsonResult(new { success = false, error = "No image provided." });

        var dbPath = _config["PlateSolver:QuadDatabasePath"];
        if (string.IsNullOrWhiteSpace(dbPath))
            return new JsonResult(new { success = false, error = "PlateSolver:QuadDatabasePath is not configured in appsettings.json." });

        var ext = Path.GetExtension(image.FileName).ToLowerInvariant().TrimStart('.');
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "." + ext);

        try
        {
            await using (var fs = System.IO.File.Create(tempPath))
                await image.CopyToAsync(fs);

            var quadDb = new CompactQuadDatabase().UseDataSource(dbPath);
            var solver = new Solver()
                .UseQuadDatabase(quadDb)
                .UseImageReader<CommonFormatsImageReader>(() => new CommonFormatsImageReader(), "jpg", "jpeg", "png");

            var strategy = new BlindSearchStrategy(new BlindSearchStrategyOptions
            {
                UseParallelism = true,
                MaxNegativeDensityOffset = 2,
                MaxPositiveDensityOffset = 2
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var result = await solver.SolveFieldAsync(tempPath, strategy, new SolverOptions(), cts.Token);

            if (!result.Success)
                return new JsonResult(new { success = false, error = "No plate solution found." });

            return new JsonResult(new
            {
                success = true,
                ra = result.Solution.PlateCenter.Ra,
                dec = result.Solution.PlateCenter.Dec,
                fieldRadius = result.Solution.Radius,
                orientation = result.Solution.Orientation,
                pixelScale = result.Solution.PixelScale,
                timeSpent = result.TimeSpent.TotalSeconds
            });
        }
        catch (OperationCanceledException)
        {
            return new JsonResult(new { success = false, error = "Plate solve timed out (5 min limit)." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plate solve failed for {FileName}", image.FileName);
            return new JsonResult(new { success = false, error = ex.Message });
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }
}
