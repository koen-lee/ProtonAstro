using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelescopeDrive.Services;
using Xunit;

namespace TelescopeDrive.Tests;

public class SolverCalibrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    // Fixed result the stub solver will return
    private static readonly SolveResult FakeSolve = new(
        Ra: 83.82,
        Dec: -5.39,
        FieldRadius: 1.2,
        Orientation: 45.0,
        PixelScale: 2.5,
        TimeSpent: TimeSpan.FromSeconds(1.5));

    private readonly WebApplicationFactory<Program> _factory;

    public SolverCalibrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                // Use simulated G-code (no serial port) and a non-null solver path
                // to pass the null-check in the page model
                cfg.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Observer:SerialPort"] = "simulated",
                    ["PlateSolver:QuadDatabasePath"] = "test",
                });
            });

            builder.ConfigureServices(services =>
            {
                // Replace real solver with stub that returns a fixed result
                services.AddSingleton<ISolverService>(new StubSolverService(FakeSolve));
            });
        });
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// Full pipeline: POST image → solver returns fixed RA/Dec → JS would invoke
    /// CalibrateRaDec → hub broadcasts CalibrationComplete to all clients.
    /// </summary>
    [Fact]
    public async Task Solve_ThenCalibrateRaDec_BroadcastsCalibrationComplete()
    {
        var client = _factory.CreateClient();

        // --- Step 1: POST a dummy image to the solve endpoint ---
        // The stub solver ignores the image bytes; EXIF extraction fails silently.
        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xD9]); // minimal JPEG stub
        imageContent.Headers.ContentType = new("image/jpeg");
        form.Add(imageContent, "image", "sample.jpg");

        var httpResponse = await client.PostAsync("/Calibration?handler=Solve", form);
        httpResponse.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await httpResponse.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean(), "Expected success:true from solve endpoint");
        var ra = root.GetProperty("ra").GetDouble();
        var dec = root.GetProperty("dec").GetDouble();
        Assert.Equal(FakeSolve.Ra, ra);
        Assert.Equal(FakeSolve.Dec, dec);
        Assert.Equal(FakeSolve.FieldRadius, root.GetProperty("fieldRadius").GetDouble());
        Assert.Equal(FakeSolve.PixelScale, root.GetProperty("pixelScale").GetDouble());
        Assert.Equal(FakeSolve.Orientation, root.GetProperty("orientation").GetDouble());

        // --- Step 2: Connect a SignalR client and subscribe to CalibrationComplete ---
        var hubUrl = new Uri(_factory.Server.BaseAddress, "/hubs/telescope");
        await using var hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, opts => opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        var calibrationTcs = new TaskCompletionSource<(string label, double alt, double az)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<string, double, double>("CalibrationComplete",
            (label, alt, az) => calibrationTcs.TrySetResult((label, alt, az)));

        await hub.StartAsync();

        // --- Step 3: Invoke CalibrateRaDec — mirrors what solvercalibration.js does ---
        await hub.InvokeAsync("CalibrateRaDec", ra, dec, (DateTimeOffset?)null);

        // --- Step 4: Assert the hub broadcast CalibrationComplete ---
        var winner = await Task.WhenAny(calibrationTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(winner == calibrationTcs.Task, "CalibrationComplete was not received within 5 seconds");

        var (label, alt, az) = await calibrationTcs.Task;
        Assert.Contains($"{ra:F4}", label);
        Assert.Contains($"{dec:F4}", label);
        Assert.True(double.IsFinite(alt), "Expected a finite altitude");
        Assert.True(double.IsFinite(az), "Expected a finite azimuth");
    }

    /// <summary>
    /// SetClockOffset: server acknowledges the new time and the reported server time
    /// reflects the requested offset.
    /// </summary>
    [Fact]
    public async Task SetClockOffset_BroadcastsAdjustedServerTime()
    {
        var hubUrl = new Uri(_factory.Server.BaseAddress, "/hubs/telescope");
        await using var hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, opts => opts.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        var tcs = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<long>("ClockOffsetApplied", serverUtcMs => tcs.TrySetResult(serverUtcMs));

        await hub.StartAsync();

        // Pretend the browser clock is 2 minutes ahead of real UTC
        var offsetMs = (long)TimeSpan.FromMinutes(2).TotalMilliseconds;
        var browserUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + offsetMs;

        await hub.InvokeAsync("SetClockOffset", browserUtcMs);

        var winner = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(winner == tcs.Task, "ClockOffsetApplied was not received within 5 seconds");

        var returnedServerUtcMs = await tcs.Task;
        var skewMs = Math.Abs(returnedServerUtcMs - browserUtcMs);
        // Allow 2 seconds of execution tolerance
        Assert.True(skewMs < 2_000,
            $"Expected server time near {browserUtcMs} ms, got {returnedServerUtcMs} ms (skew {skewMs} ms)");
    }
}

// ---------------------------------------------------------------------------

file sealed class StubSolverService(SolveResult result) : ISolverService
{
    public Task<SolveResult> SolveAsync(string imagePath, SolveHint hint = null, CancellationToken ct = default)
    => Task.FromResult(result);
}
