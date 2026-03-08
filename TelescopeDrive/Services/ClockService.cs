namespace TelescopeDrive.Services;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    void SetOffset(TimeSpan offset);
}

/// <summary>
/// Application clock with an adjustable offset applied on top of the system UTC clock.
/// Intended as a band-aid for devices without an RTC (e.g. Raspberry Pi on a local-only
/// network with no NTP access). The offset is in-memory only; it resets on restart.
/// The real fix is NTP synchronisation or adding an RTC module.
/// </summary>
public class ClockService : IClock
{
    private TimeSpan _offset = TimeSpan.Zero;

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow + _offset;

    public void SetOffset(TimeSpan offset) => _offset = offset;
}
