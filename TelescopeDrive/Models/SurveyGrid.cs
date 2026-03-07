namespace TelescopeDrive.Models;

/// <summary>
/// Generates the canonical all-sky survey grid for multipoint mount calibration.
///
/// Grid layout:
///   Ring 1 (alt = 30°): 8 points every 45° in azimuth
///   Ring 2 (alt = 60°): 4 points every 90° in azimuth
///   Near-zenith (alt = 80°): 1 point at az = 0° (avoids singularity at true zenith)
///
/// Points below MinAltDeg are excluded. The model is usable from 3 recorded points —
/// the user may skip any grid points they cannot reach or do not need.
/// </summary>
public static class SurveyGrid
{
    public const double MinAltDeg = 15.0;

    public static IReadOnlyList<(double AltDeg, double AzDeg)> Generate(
        double minAltDeg = MinAltDeg)
    {
        var points = new List<(double, double)>();

        for (int i = 0; i < 8; i++)
            points.Add((30.0, i * 45.0));

        for (int i = 0; i < 4; i++)
            points.Add((60.0, i * 90.0));

        points.Add((80.0, 0.0));

        return points
            .Where(p => p.Item1 >= minAltDeg)
            .ToList()
            .AsReadOnly();
    }
}
