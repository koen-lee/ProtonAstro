using Microsoft.Extensions.Logging.Abstractions;
using ProtonAstroLib;
using TelescopeDrive.Models;
using TelescopeDrive.Services;
using Xunit;

namespace TelescopeDrive.Tests;

public class AlignmentModelTests : IDisposable
{
    // Each test gets its own temp file so tests are isolated and don't touch LocalApplicationData.
    private readonly string _tempFile =
        Path.Combine(Path.GetTempPath(), $"alignment-test-{Guid.NewGuid()}.json");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private AlignmentModel Create() =>
        new(NullLogger<AlignmentModel>.Instance, _tempFile);

    private static AlignmentPoint P(double alt, double az, double dAlt, double dAz) =>
        new(alt, az, dAlt, dAz, DateTimeOffset.UtcNow);

    private static HorizontalCoordinate H(double alt, double az) =>
        new(Angle.FromDegrees(alt), Angle.FromDegrees(az));

    // ── GetCorrection: edge cases ─────────────────────────────────────────

    [Fact]
    public void GetCorrection_NoPoints_ReturnsZero()
    {
        var (dAlt, dAz) = Create().GetCorrection(H(45.0, 180.0));
        Assert.Equal(0.0, dAlt.Degrees);
        Assert.Equal(0.0, dAz.Degrees);
    }

    [Fact]
    public void GetCorrection_OnePoint_ReturnsItsDeltaEverywhere()
    {
        var model = Create();
        model.AddPoint(P(30.0, 90.0, 0.5, -0.3));

        // Query at a completely different sky position — single-point acts like a global offset
        var (dAlt, dAz) = model.GetCorrection(H(60.0, 270.0));
        Assert.Equal(0.5, dAlt.Degrees);
        Assert.Equal(-0.3, dAz.Degrees);
    }

    [Fact]
    public void GetCorrection_AtExactCalibrationPoint_ReturnsThatPointsDelta()
    {
        var model = Create();
        model.AddPoint(P(45.0, 180.0, 1.2, -0.8));
        model.AddPoint(P(30.0, 90.0,  0.5,  0.2));

        // Querying exactly at a calibration point triggers the singularity guard
        var (dAlt, dAz) = model.GetCorrection(H(45.0, 180.0));
        Assert.Equal(1.2, dAlt.Degrees, precision: 6);
        Assert.Equal(-0.8, dAz.Degrees, precision: 6);
    }

    // ── GetCorrection: IDW weighting ──────────────────────────────────────

    [Fact]
    public void GetCorrection_Midpoint_WeightsSymmetrically()
    {
        // Two points equidistant from the query, equal and opposite alt deltas.
        // The weighted average should cancel to ~0.
        var model = Create();
        model.AddPoint(P(30.0, 0.0,  1.0, 0.0));
        model.AddPoint(P(60.0, 0.0, -1.0, 0.0));

        var (dAlt, _) = model.GetCorrection(H(45.0, 0.0));
        Assert.Equal(0.0, dAlt.Degrees, precision: 10);
    }

    [Fact]
    public void GetCorrection_NearPoint_DominatesWeight()
    {
        // One calibration point very close to the query, another far away.
        // The correction should be pulled strongly toward the near point's delta.
        var model = Create();
        model.AddPoint(P(30.0, 0.0, 1.0, 0.0));  // near query
        model.AddPoint(P(80.0, 0.0, 0.0, 0.0));  // far from query

        var (dAlt, _) = model.GetCorrection(H(30.1, 0.0));
        Assert.True(dAlt.Degrees > 0.9, $"Expected correction close to 1.0, got {dAlt.Degrees:F6}");
    }

    [Fact]
    public void GetCorrection_FarFromAllPoints_SmoothlyInterpolates()
    {
        // Three points all with +1° alt error → any query should also return ~+1°
        var model = Create();
        model.AddPoint(P(20.0,   0.0, 1.0, 0.0));
        model.AddPoint(P(20.0, 120.0, 1.0, 0.0));
        model.AddPoint(P(20.0, 240.0, 1.0, 0.0));

        var (dAlt, _) = model.GetCorrection(H(60.0, 60.0));
        Assert.Equal(1.0, dAlt.Degrees, precision: 6);
    }

    // ── GetCorrection: azimuth wrap-around ────────────────────────────────

    [Fact]
    public void GetCorrection_AzimuthWrap_SymmetricErrorCancels()
    {
        // One point just below 360° with a negative az delta,
        // one just above 0° with a positive az delta.
        // A query at 0° lies midway — the circular mean should give ~0°.
        var model = Create();
        model.AddPoint(P(45.0, 358.0, 0.0, -2.0));
        model.AddPoint(P(45.0,   2.0, 0.0,  2.0));

        var (_, dAz) = model.GetCorrection(H(45.0, 0.0));
        Assert.Equal(0.0, dAz.Degrees, precision: 10);
    }

    [Fact]
    public void GetCorrection_AzimuthWrap_NaiveMeanWouldBeWrong()
    {
        // Both points have az delta that straddles 0°/360°.
        // A naive (non-circular) mean of 358° and 2° gives 180° — clearly wrong.
        // The circular mean gives 0°, which is correct.
        var model = Create();
        model.AddPoint(P(45.0,  0.0, 0.0, 358.0));  // delta: -2° expressed as 358°
        model.AddPoint(P(45.0, 90.0, 0.0, 358.0));

        // Both points have the same az delta so the result should equal that delta
        // after the circular mean maps 358° → -2°.
        // Note: AddAlignmentPoint in the hub normalises to [-180, 180] before storing,
        // so this tests what happens if data were stored without normalisation.
        // The circular mean correctly maps 358° to ~-2°.
        var (_, dAz) = model.GetCorrection(H(45.0, 45.0));
        // atan2(sin(358°_rad), cos(358°_rad)) ≈ -2°
        Assert.True(dAz.Degrees < -1.0 && dAz.Degrees > -3.0,
            $"Expected az correction near -2° (circular mean of 358°), got {dAz.Degrees:F4}°");
    }

    // ── Mutation: Clear ───────────────────────────────────────────────────

    [Fact]
    public void Clear_RemovesAllPoints_CorrectionReturnsZero()
    {
        var model = Create();
        model.AddPoint(P(45.0,  90.0, 1.0,  0.5));
        model.AddPoint(P(60.0, 180.0, 0.5, -0.5));

        model.Clear();

        Assert.Empty(model.Points);
        var (dAlt, dAz) = model.GetCorrection(H(45.0, 90.0));
        Assert.Equal(0.0, dAlt.Degrees);
        Assert.Equal(0.0, dAz.Degrees);
    }

    // ── Thread safety ─────────────────────────────────────────────────────

    [Fact]
    public async Task AddPoint_ConcurrentAdds_AllPointsPresent()
    {
        var model = Create();
        const int count = 50;

        await Task.WhenAll(Enumerable.Range(0, count).Select(i =>
            Task.Run(() => model.AddPoint(P(i * 3.0 % 75, i * 7.0 % 360, 0.1, 0.1)))));

        Assert.Equal(count, model.Points.Count);
    }

    // ── Persistence ───────────────────────────────────────────────────────

    [Fact]
    public void Persistence_PointsSurviveRoundTrip()
    {
        var point = P(30.0, 90.0, 1.5, -0.7);

        var model1 = Create();
        model1.AddPoint(point);

        // Simulate restart: new instance loading from the same file
        var model2 = new AlignmentModel(NullLogger<AlignmentModel>.Instance, _tempFile);

        Assert.Single(model2.Points);
        Assert.Equal(point.ExpectedAltDeg, model2.Points[0].ExpectedAltDeg);
        Assert.Equal(point.ExpectedAzDeg,  model2.Points[0].ExpectedAzDeg);
        Assert.Equal(point.DeltaAltDeg,    model2.Points[0].DeltaAltDeg);
        Assert.Equal(point.DeltaAzDeg,     model2.Points[0].DeltaAzDeg);
    }

    [Fact]
    public void Persistence_MultiplePoints_AllSurviveRoundTrip()
    {
        var model1 = Create();
        model1.AddPoint(P(30.0,   0.0, 0.3,  0.1));
        model1.AddPoint(P(30.0,  90.0, 0.4, -0.2));
        model1.AddPoint(P(60.0, 180.0, 0.2,  0.3));

        var model2 = new AlignmentModel(NullLogger<AlignmentModel>.Instance, _tempFile);

        Assert.Equal(3, model2.Points.Count);
    }

    [Fact]
    public void Persistence_Clear_EmptiesPersistedFile()
    {
        var model1 = Create();
        model1.AddPoint(P(30.0, 90.0, 1.5, -0.7));
        model1.Clear();

        var model2 = new AlignmentModel(NullLogger<AlignmentModel>.Instance, _tempFile);

        Assert.Empty(model2.Points);
    }

    [Fact]
    public void Persistence_MissingFile_StartsEmpty()
    {
        // _tempFile was never written — model should start with zero points
        var model = Create();

        Assert.Empty(model.Points);
        var (dAlt, dAz) = model.GetCorrection(H(45.0, 90.0));
        Assert.Equal(0.0, dAlt.Degrees);
        Assert.Equal(0.0, dAz.Degrees);
    }
}
