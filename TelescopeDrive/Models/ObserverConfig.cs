using ProtonAstroLib;

namespace TelescopeDrive.Models;

public class ObserverConfig
{
    /// <summary>
    /// Observer's latitude in degrees. This is used for calculating the target's position in the sky based on the current time and the target's celestial coordinates.
    /// </summary>
    public double LatitudeDegrees { get; set; } = 51.92;
    /// <summary>
    /// Observer's longitude in degrees. This is used for calculating the target's position in the sky based on the current time and the target's celestial coordinates.
    /// </summary>
    public double LongitudeDegrees { get; set; } = 4.26;
    /// <summary>
    /// Serial port name for the telescope controller. Set to "simulated" to use the in-process SimulatedGCodeService for testing without hardware.
    /// eg "COM3" on Windows, "/dev/ttyUSB0" on Linux.
    /// </summary>
    public string SerialPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 115200;
    /// <summary>
    /// Interval in milliseconds between tracking updates. This determines how often the controller receives position updates and adjusts its movement.
    /// </summary>
    public int TrackingIntervalMs { get; set; } = 5000;
    /// <summary>
    /// Maximum feedrate in degrees per minute. 
    /// This is a safety limit to prevent the controller from trying to execute an excessively fast move due to moves through the zenith,
    /// a bug or bad ephemeris data. The default of 360 deg/min means the telescope can do a full 360° azimuth slew in one minute, which should be sufficient for any normal tracking or slewing operation.
    /// This is handware-dependent: if your telescope can safely do faster moves, you can increase this limit. If you have a slow or fragile mount, you might want to decrease it. 
    /// The tracking algorithm will adjust feedrates to try to stay under this limit, but if the target moves faster than this (e.g. near zenith) it will still attempt the move at the max feedrate.
    /// </summary>
    public double MaxFeedrateDegPerMin { get; set; } = 360.0;

    public WGS84Coordinate ToWGS84() =>
        new(Angle.FromDegrees(LatitudeDegrees), Angle.FromDegrees(LongitudeDegrees));
}
