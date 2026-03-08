using TelescopeDrive.Services;
using Xunit;

namespace TelescopeDrive.Tests;

public class ClockServiceTests
{
    [Fact]
    public void UtcNow_DefaultOffset_IsCloseToSystemUtc()
    {
        var clock = new ClockService();
        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(now, before, after);
    }

    [Fact]
    public void SetOffset_PositiveOffset_AdvancesClock()
    {
        var clock = new ClockService();
        var offset = TimeSpan.FromHours(2);
        clock.SetOffset(offset);

        var skew = clock.UtcNow - DateTimeOffset.UtcNow;

        // Allow 1 second of execution tolerance
        Assert.InRange(skew.TotalSeconds, offset.TotalSeconds - 1, offset.TotalSeconds + 1);
    }

    [Fact]
    public void SetOffset_NegativeOffset_RetardsClock()
    {
        var clock = new ClockService();
        var offset = TimeSpan.FromDays(-90); // 3-month-dead Pi scenario
        clock.SetOffset(offset);

        var skew = clock.UtcNow - DateTimeOffset.UtcNow;

        Assert.InRange(skew.TotalSeconds, offset.TotalSeconds - 1, offset.TotalSeconds + 1);
    }

    [Fact]
    public void SetOffset_OverridesPreviousOffset()
    {
        var clock = new ClockService();
        clock.SetOffset(TimeSpan.FromHours(12));
        clock.SetOffset(TimeSpan.Zero);

        var skew = Math.Abs((clock.UtcNow - DateTimeOffset.UtcNow).TotalSeconds);
        Assert.True(skew < 1, $"Expected near-zero skew after reset, got {skew:F3}s");
    }
}
