using Microsoft.AspNetCore.SignalR;
using ProtonAstroLib;
using TelescopeDrive.Models;
using TelescopeDrive.Services;

namespace TelescopeDrive.Hubs;

public class TelescopeHub : Hub
{
    private readonly ITrackingService _tracking;
    private readonly IGCodeService _gcode;
    private readonly ISerialPortService _serial;
    private readonly IAlignmentModel _alignment;
    private readonly IClock _clock;

    public TelescopeHub(ITrackingService tracking, IGCodeService gcode, ISerialPortService serial, IAlignmentModel alignment, IClock clock)
    {
        _tracking = tracking;
        _gcode = gcode;
        _serial = serial;
        _alignment = alignment;
        _clock = clock;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ConnectionStatus", _serial.IsConnected, _serial.CurrentPort, _serial.IsSimulated);
        await Clients.Caller.SendAsync("TrackingStatus", _tracking.State.IsTracking);
        await Clients.Caller.SendAsync("AlignmentModelStatus", _alignment.Points.Count);
        await Clients.Caller.SendAsync("ObserverLocationSet",
            _tracking.Observer.Latitude.Degrees,
            _tracking.Observer.Longitude.Degrees);

        await BroadcastPositionAsync(Clients.Caller);

        await base.OnConnectedAsync();
    }

    private async Task BroadcastPositionAsync(IClientProxy target)
    {
        if (await _gcode.QueryRealtimePositionAsync() is { } queried)
        {
            var now = _clock.UtcNow;

            var (corrAlt, corrAz) = _alignment.GetCorrection(new HorizontalCoordinate(queried.alt, queried.az));
            // this is not actually the inverse correction, but it's a useful approximation because corrections are small and the coordinate transform is mostly linear over small angles
            var horizontalSky = new HorizontalCoordinate(
                queried.alt + corrAlt,
                queried.az + corrAz);

            var eq = horizontalSky.ToEquatorialCoordinate(now, _tracking.Observer);

            var trackingList = _tracking.State.IsTracking && _tracking.State.TargetFunc != null
                ? _tracking.State.GetTrackingList(now, _tracking.Observer)
                : [];

            var orientationStars = TrackingState.GetOrientationStars(now, _tracking.Observer);

            await target.SendAsync("PositionUpdate",
                queried.alt.Degrees, queried.az.Degrees,
                _tracking.State.TargetName, _tracking.State.IsTracking,
                eq.RightAscension.Degrees, eq.Declination.Degrees,
                trackingList, orientationStars);
        }
    }

    public async Task SendGCode(string gcode)
    {
        await _gcode.SendRawAsync(gcode);
    }

    public async Task Goto(string targetKey)
    {
        if (SolarSystem.TryGetBody(targetKey, out var bodyFunc))
        {
            _tracking.SetTarget(bodyFunc, targetKey);
        }
        else
        {
            var entry = CatalogEntries.FindByName(targetKey);
            if (entry == null) return;
            // Fixed star: same coordinate regardless of time
            var coord = entry.Coordinate;
            _tracking.SetTarget(_ => coord, entry.Name);
        }
        await _tracking.GotoAsync();
        await BroadcastPositionAsync(Clients.All);
    }

    public async Task GotoCustom(string ra, string dec)
    {
        var coord = EquatorialCoordinate.FromRaDec(ra, dec);
        _tracking.SetTarget(_ => coord, $"Custom ({ra}, {dec})");
        await _tracking.GotoAsync();
        await BroadcastPositionAsync(Clients.All);
    }

    public async Task StartTracking()
    {
        _tracking.StartTracking();
        await Clients.All.SendAsync("TrackingStatus", true);
        await BroadcastPositionAsync(Clients.All);
    }

    public async Task StopTracking()
    {
        _tracking.StopTracking();
        await Clients.All.SendAsync("TrackingStatus", false);
    }

    public async Task Home(string axis)
    {
        var cmd = axis.ToLowerInvariant() switch
        {
            "x" => GCodeCommand.HomeX,
            "y" => GCodeCommand.HomeY,
            _ => GCodeCommand.HomeAll,
        };
        await _gcode.SendCommandAsync(cmd);
    }

    public async Task Jog(string direction, double stepDeg)
    {
        var (dAlt, dAz) = direction.ToLowerInvariant() switch
        {
            "up" => (stepDeg, 0.0),
            "down" => (-stepDeg, 0.0),
            "right" => (0.0, stepDeg),
            "left" => (0.0, -stepDeg),
            _ => (0.0, 0.0),
        };
        await _tracking.JogAsync(dAlt, dAz);
        await BroadcastPositionAsync(Clients.All);
    }

    public void AdoptPosition()
    {
        _tracking.AdoptPosition();
    }

    public async Task CalibrateRaDec(double ra, double dec, DateTimeOffset? imageEpoch = null)
    {
        var coord = new EquatorialCoordinate(
            Angle.FromDegrees(ra),
            Angle.FromDegrees(dec));

        var now = imageEpoch ?? _clock.UtcNow;
        var horizontal = coord.GetHorizontalCoordinate(now, _tracking.Observer);

        await _gcode.SendCommandAsync(GCodeCommand.SetPosition(
            horizontal.Altitude.Degrees, horizontal.Azimuth.Degrees));

        _tracking.State.LastCommandedPosition = horizontal;
        _tracking.State.LastUpdateTime = now;

        await Clients.All.SendAsync("CalibrationComplete",
            $"Plate solve (RA {ra:F4}°, Dec {dec:F4}°)",
            horizontal.Altitude.Degrees, horizontal.Azimuth.Degrees);
    }

    public async Task Calibrate(string starName)
    {
        var entry = CatalogEntries.FindByName(starName);
        if (entry == null) return;

        var now = _clock.UtcNow;
        var horizontal = entry.Coordinate.GetHorizontalCoordinate(now, _tracking.Observer);

        await _gcode.SendCommandAsync(GCodeCommand.SetPosition(
            horizontal.Altitude.Degrees, horizontal.Azimuth.Degrees));

        _tracking.State.LastCommandedPosition = horizontal;
        _tracking.State.LastUpdateTime = now;

        await Clients.All.SendAsync("CalibrationComplete",
            starName, horizontal.Altitude.Degrees, horizontal.Azimuth.Degrees);
    }

    public async Task Connect(string port, int baud)
    {
        await _serial.ConnectAsync(port, baud);
        await Clients.All.SendAsync("ConnectionStatus", _serial.IsConnected, _serial.CurrentPort, _serial.IsSimulated);
    }

    public async Task Disconnect()
    {
        _tracking.StopTracking();
        await _serial.DisconnectAsync();
        await Clients.All.SendAsync("ConnectionStatus", false, null, false);
        await Clients.All.SendAsync("TrackingStatus", false);
    }

    public async Task SetObserverLocation(double lat, double lon)
    {
        _tracking.SetObserverLocation(lat, lon);
        await Clients.All.SendAsync("ObserverLocationSet", lat, lon);
    }

    public Task GetObserverLocation() =>
        Clients.Caller.SendAsync("ObserverLocationSet",
            _tracking.Observer.Latitude.Degrees,
            _tracking.Observer.Longitude.Degrees);

    public Task GetAvailablePorts()
    {
        return Clients.Caller.SendAsync("AvailablePorts", _serial.AvailablePorts);
    }

    // ── Multipoint calibration ────────────────────────────────────────────

    public Task GetSurveyGrid()
    {
        var grid = SurveyGrid.Generate()
            .Select(p => new { altDeg = p.AltDeg, azDeg = p.AzDeg })
            .ToArray();
        return Clients.Caller.SendAsync("SurveyGrid", (object)grid);
    }

    /// <summary>
    /// Slew to a survey grid position without applying the alignment correction.
    /// This is intentional: the purpose is to measure the raw pointing error at
    /// each position, so corrections must not be pre-applied.
    /// </summary>
    public async Task GotoSurveyPoint(double altDeg, double azDeg)
    {
        await _gcode.SendCommandAsync(GCodeCommand.AbsoluteMove(altDeg, azDeg));
        _tracking.State.LastCommandedPosition =
            new HorizontalCoordinate(Angle.FromDegrees(altDeg), Angle.FromDegrees(azDeg));
        _tracking.State.LastUpdateTime = _clock.UtcNow;
        await Clients.All.SendAsync("SurveyPointReached", altDeg, azDeg);
    }

    /// <summary>
    /// Record one alignment point from a plate solve result.
    /// expectedAltDeg/expectedAzDeg are the survey grid coordinates we slewed to.
    /// ra/dec are the plate-solved actual sky coordinates (degrees).
    /// </summary>
    public async Task AddAlignmentPoint(
        double ra, double dec,
        double expectedAltDeg, double expectedAzDeg,
        DateTimeOffset? imageEpoch = null)
    {
        var now = imageEpoch ?? _clock.UtcNow;
        var actualCoord = new EquatorialCoordinate(Angle.FromDegrees(ra), Angle.FromDegrees(dec));
        var actualHorizontal = actualCoord.GetHorizontalCoordinate(now, _tracking.Observer);

        var deltaAlt = actualHorizontal.Altitude.Degrees - expectedAltDeg;
        var deltaAz  = actualHorizontal.Azimuth.Degrees  - expectedAzDeg;
        if (deltaAz >  180) deltaAz -= 360;
        if (deltaAz < -180) deltaAz += 360;

        var point = new AlignmentPoint(expectedAltDeg, expectedAzDeg, deltaAlt, deltaAz, now);
        _alignment.AddPoint(point);

        await Clients.All.SendAsync("AlignmentPointAdded",
            expectedAltDeg, expectedAzDeg, deltaAlt, deltaAz, _alignment.Points.Count);
    }

    public async Task ClearAlignmentModel()
    {
        _alignment.Clear();
        await Clients.All.SendAsync("AlignmentModelCleared");
    }

    /// <summary>
    /// Adjusts the application clock so it matches the browser's UTC time.
    /// Intended for devices without an RTC that have drifted while offline.
    /// The offset is in-memory only and resets on restart.
    /// </summary>
    public async Task SetClockOffset(long browserUtcMs)
    {
        var browserTime = DateTimeOffset.FromUnixTimeMilliseconds(browserUtcMs);
        _clock.SetOffset(browserTime - DateTimeOffset.UtcNow);
        await Clients.Caller.SendAsync("ClockOffsetApplied", _clock.UtcNow.ToUnixTimeMilliseconds());
    }
}
