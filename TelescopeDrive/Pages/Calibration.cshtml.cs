using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using TelescopeDrive.Services;

namespace TelescopeDrive.Pages;

[IgnoreAntiforgeryToken]
public class CalibrationModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly ISolverService _solver;
    private readonly ILogger<CalibrationModel> _logger;

    public CalibrationModel(IConfiguration config, ISolverService solver, ILogger<CalibrationModel> logger)
    {
        _config = config;
        _solver = solver;
        _logger = logger;
    }

    public async Task<IActionResult> OnPostSolveAsync(IFormFile image)
    {
        if (image == null || image.Length == 0)
            return new JsonResult(new { success = false, error = "No image provided." });
        var fallbackTimestamp = DateTimeOffset.UtcNow;
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

            var meta = ext is "jpg" or "jpeg" or "png"
                ? ExtractExifMeta(tempPath)
                : (GpsLat: (double?)null, GpsLon: (double?)null, Epoch: (DateTimeOffset?)null, EpochSource: (string?)null);

            using var cts = new CancellationTokenSource(timeout);
            var result = await _solver.SolveAsync(tempPath, cts.Token);

            if (result == null)
                return new JsonResult(new { success = false, error = "No plate solution found." });

            var (imageEpoch, epochSource) = meta.Epoch.HasValue
                ? (meta.Epoch, meta.EpochSource)
                : (fallbackTimestamp, "Request timestamp (inaccurate)");

            return new JsonResult(new
            {
                success = true,
                ra = result.Ra,
                dec = result.Dec,
                fieldRadius = result.FieldRadius,
                orientation = result.Orientation,
                pixelScale = result.PixelScale,
                timeSpent = result.TimeSpent.TotalSeconds,
                imageEpoch,
                epochSource,
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

    private (double? GpsLat, double? GpsLon, DateTimeOffset? Epoch, string? EpochSource) ExtractExifMeta(string filePath)
    {
        try
        {
            using var img = Image.Load(filePath);
            var exif = img.Metadata.ExifProfile;
            if (exif == null) return (null, null, null, null);

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

            DateTimeOffset? epoch = null;
            string? epochSource = null;

            exif.TryGetValue(ExifTag.GPSDateStamp, out var gpsDateVal);
            exif.TryGetValue(ExifTag.GPSTimestamp, out var gpsTimeVal);
            var gpsDate = gpsDateVal?.Value;
            var gpsTime = gpsTimeVal?.Value;

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
                    epochSource = "gps, reliable";
                }
            }

            if (epoch == null)
            {
                exif.TryGetValue(ExifTag.DateTimeOriginal, out var dtoVal);
                exif.TryGetValue(ExifTag.OffsetTimeOriginal, out var offsetVal);

                var dto = dtoVal?.Value;
                var off = offsetVal?.Value;

                if (dto != null)
                {
                    var parts = dto.Split(new char[] { ':', ' ' });
                    if (parts.Length == 6 &&
                        int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var mo) &&
                        int.TryParse(parts[2], out var d) && int.TryParse(parts[3], out var h) &&
                        int.TryParse(parts[4], out var mi) && int.TryParse(parts[5], out var s))
                    {
                        if (off != null && TryParseUtcOffset(off, out var offset))
                        {
                            epoch = new DateTimeOffset(y, mo, d, h, mi, s, offset);
                            epochSource = "offsetoriginal, reliable if camera clock is correct";
                        }
                        else
                        {
                            epoch = new DateTimeOffset(y, mo, d, h, mi, s, TimeSpan.Zero);
                            epochSource = "datetimeoriginal, might be in unknown timezone (assumed UTC here)";
                        }
                    }
                }
            }

            return (lat, lon, epoch, epochSource);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract EXIF metadata from {FilePath}", filePath);
            return (null, null, null, null);
        }
    }

    private static bool TryParseUtcOffset(string s, out TimeSpan offset)
    {
        offset = default;
        if (s.Length < 6) return false;
        var sign = s[0] == '-' ? -1 : 1;
        if (!int.TryParse(s.Substring(1, 2), out var h)) return false;
        if (!int.TryParse(s.Substring(4, 2), out var m)) return false;
        offset = new TimeSpan(sign * h, sign * m, 0);
        return true;
    }

    private static double RatToDeg(Rational[] r) =>
        r[0].ToDouble() + r[1].ToDouble() / 60.0 + r[2].ToDouble() / 3600.0;
}
