using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using WatneyAstrometry.Core;
using WatneyAstrometry.Core.QuadDb;
using WatneyAstrometry.ImageReaders;

namespace TelescopeDrive.Pages;

[IgnoreAntiforgeryToken]
[RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
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

        TimeSpan timeout = TimeSpan.FromSeconds(30);

        try
        {
            await using (var fs = System.IO.File.Create(tempPath))
                await image.CopyToAsync(fs);

            // Extract EXIF before handing the file to Watney
            var meta = ext is "jpg" or "jpeg" or "png"
                ? ExtractExifMeta(tempPath)
                : (GpsLat: (double?)null, GpsLon: (double?)null, Epoch: (DateTimeOffset?)null);

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

            using var cts = new CancellationTokenSource(timeout);
            var result = await solver.SolveFieldAsync(tempPath, strategy, new WatneyAstrometry.Core.Types.SolverOptions(), cts.Token);

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
                timeSpent = result.TimeSpent.TotalSeconds,
                imageEpoch = meta.Epoch,
                gpsLat = meta.GpsLat,
                gpsLon = meta.GpsLon
            });
        }
        catch (OperationCanceledException)
        {
            return new JsonResult(new { success = false, error = $"Plate solve timed out after {timeout.TotalSeconds} seconds — try a clearer image." });
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

    private (double? GpsLat, double? GpsLon, DateTimeOffset? Epoch) ExtractExifMeta(string filePath)
    {
        try
        {
            using var img = Image.Load(filePath);
            var exif = img.Metadata.ExifProfile;
            if (exif == null) return (null, null, null);

            // GPS coordinates
            double? lat = null, lon = null;
            exif.TryGetValue(ExifTag.GPSLatitude, out var rawLatVal);
            exif.TryGetValue(ExifTag.GPSLatitudeRef, out var latRefVal);
            exif.TryGetValue(ExifTag.GPSLongitude, out var rawLonVal);
            exif.TryGetValue(ExifTag.GPSLongitudeRef, out var lonRefVal);
            var rawLat = rawLatVal?.Value;
            var latRef = latRefVal?.Value;
            var rawLon = rawLonVal?.Value;
            var lonRef = lonRefVal?.Value;

            if (rawLat is { Length: 3 } && rawLon is { Length: 3 })
            {
                lat = RatToDeg(rawLat);
                if (latRef == "S") lat = -lat;
                lon = RatToDeg(rawLon);
                if (lonRef == "W") lon = -lon;
            }

            // Timestamp — prefer GPS date+time (unambiguously UTC) over DateTimeOriginal (local, no tz)
            DateTimeOffset? epoch = null;

            exif.TryGetValue(ExifTag.GPSDateStamp, out var gpsDateVal);
            exif.TryGetValue(ExifTag.GPSTimestamp, out var gpsTimeVal);
            var gpsDate = gpsDateVal?.Value;  // "YYYY:MM:DD"
            var gpsTime = gpsTimeVal?.Value;  // Rational[] {H, M, S}

            if (gpsDate != null && gpsTime is { Length: 3 })
            {
                var p = gpsDate.Split(':');
                if (p.Length == 3 &&
                    int.TryParse(p[0], out var y) &&
                    int.TryParse(p[1], out var mo) &&
                    int.TryParse(p[2], out var d))
                {
                    var h = (int)gpsTime[0].ToDouble();
                    var m = (int)gpsTime[1].ToDouble();
                    var s = gpsTime[2].ToDouble();
                    var ms = (int)((s - (int)s) * 1000);
                    epoch = new DateTimeOffset(y, mo, d, h, m, (int)s, ms, TimeSpan.Zero);
                }
            }

            if (epoch == null)
            {
                if (!exif.TryGetValue(ExifTag.OffsetTimeOriginal, out var dtoVal) &&
                    !exif.TryGetValue(ExifTag.DateTimeOriginal, out dtoVal)) 
                {
                    return (lat, lon, null);
                }
                // DateTimeOriginal format: "YYYY:MM:DD HH:MM:SS"
                // No timezone in EXIF — treat as UTC (best-effort; GPS timestamp above is preferred)
                var dto = dtoVal?.Value;
                if (dto != null && DateTimeOffset.TryParseExact(dto, "yyyy:MM:dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                {
                    epoch = parsed;
                }
            }

            return (lat, lon, epoch);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract EXIF metadata from {FilePath}", filePath);
            return (null, null, null);
        }
    }

    private static double RatToDeg(Rational[] r) =>
        r[0].ToDouble() + r[1].ToDouble() / 60.0 + r[2].ToDouble() / 3600.0;
}
