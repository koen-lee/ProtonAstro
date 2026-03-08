using System.Text.Json;
using ProtonAstroLib;
using TelescopeDrive.Models;

namespace TelescopeDrive.Services;

/// <summary>
/// Thread-safe IDW (Inverse Distance Weighting, power=2) alignment model.
///
/// Correction = sum(w_i * delta_i) / sum(w_i)  where  w_i = 1 / d_i²
///
/// Angular distance uses the spherical law of cosines:
///   cos(d) = sin(alt1)*sin(alt2) + cos(alt1)*cos(alt2)*cos(az1 - az2)
///
/// Azimuth correction uses circular mean (sin/cos accumulation → Atan2)
/// to correctly handle the 0°/360° wrap boundary.
///
/// Edge cases:
///   0 points  → (0, 0)
///   1 point   → return its delta everywhere (consistent with single-point behaviour)
///   d &lt; ε    → return the nearest point exactly (singularity guard)
///
/// Persistence: points are saved to LocalApplicationData on every mutation and
/// loaded on construction, so the model survives application restarts.
/// </summary>
public sealed class AlignmentModel : IAlignmentModel
{
    private const double EpsilonRad = 0.01 * Math.PI / 180.0;

    private static readonly string DefaultFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TelescopeDrive", "alignment-model.json");

    private static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly List<AlignmentPoint> _points = new();
    private readonly object _lock = new();
    private readonly object _fileLock = new();
    private readonly ILogger<AlignmentModel> _logger;

    /// <param name="logger">Logger instance.</param>
    /// <param name="filePath">Override the storage path (used in tests to avoid touching LocalApplicationData).</param>
    public AlignmentModel(ILogger<AlignmentModel> logger, string? filePath = null)
    {
        _logger = logger;
        _filePath = filePath ?? DefaultFilePath;
        Load();
    }

    public IReadOnlyList<AlignmentPoint> Points
    {
        get { lock (_lock) { return _points.ToList(); } }
    }

    public void AddPoint(AlignmentPoint point)
    {
        lock (_lock) { _points.Add(point); }
        Save();
    }

    public void Clear()
    {
        lock (_lock) { _points.Clear(); }
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var points = JsonSerializer.Deserialize<List<AlignmentPoint>>(json);
            if (points is { Count: > 0 })
            {
                lock (_lock) { _points.AddRange(points); }
                _logger.LogInformation(
                    "Loaded {Count} alignment point(s) from {Path}", points.Count, _filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to load alignment model from {Path}; starting with empty model", _filePath);
        }
    }

    private void Save()
    {
        List<AlignmentPoint> snapshot;
        lock (_lock) { snapshot = new List<AlignmentPoint>(_points); }

        lock (_fileLock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                var json = JsonSerializer.Serialize(snapshot, JsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to persist alignment model to {Path}; model remains active in memory", _filePath);
            }
        }
    }

    public (Angle DeltaAlt, Angle DeltaAz) GetCorrection(HorizontalCoordinate horizontal)
    {
        List<AlignmentPoint> snapshot;
        lock (_lock) { snapshot = new List<AlignmentPoint>(_points); }

        if (snapshot.Count == 0) return (Angle.FromDegrees(0), Angle.FromDegrees(0));
        if (snapshot.Count == 1) return (Angle.FromDegrees(snapshot[0].DeltaAltDeg), Angle.FromDegrees(snapshot[0].DeltaAzDeg));

        var qAlt = (double)horizontal.Altitude;
        var qAz  = (double)horizontal.Azimuth;

        double weightSum = 0.0;
        double altSum    = 0.0;
        double azSinSum  = 0.0;
        double azCosSum  = 0.0;

        foreach (var p in snapshot)
        {
            var pAlt = p.ExpectedAltDeg * Math.PI / 180.0;
            var pAz  = p.ExpectedAzDeg  * Math.PI / 180.0;

            var cosD = Math.Clamp(
                Math.Sin(qAlt) * Math.Sin(pAlt)
              + Math.Cos(qAlt) * Math.Cos(pAlt) * Math.Cos(qAz - pAz),
                -1.0, 1.0);
            var d = Math.Acos(cosD);

            if (d < EpsilonRad)
                return (Angle.FromDegrees(p.DeltaAltDeg), Angle.FromDegrees(p.DeltaAzDeg));

            var w = 1.0 / (d * d);
            weightSum += w;
            altSum    += w * p.DeltaAltDeg;

            var azDeltaRad = p.DeltaAzDeg * Math.PI / 180.0;
            azSinSum += w * Math.Sin(azDeltaRad);
            azCosSum += w * Math.Cos(azDeltaRad);
        }

        var corrAlt = altSum / weightSum;
        var corrAz  = Math.Atan2(azSinSum / weightSum, azCosSum / weightSum) * 180.0 / Math.PI;

        return (Angle.FromDegrees(corrAlt), Angle.FromDegrees(corrAz));
    }
}
