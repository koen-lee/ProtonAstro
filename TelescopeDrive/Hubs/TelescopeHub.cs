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

    public TelescopeHub(ITrackingService tracking, IGCodeService gcode, ISerialPortService serial)
    {
        _tracking = tracking;
        _gcode = gcode;
        _serial = serial;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ConnectionStatus", _serial.IsConnected, _serial.CurrentPort);
        await Clients.Caller.SendAsync("TrackingStatus", _tracking.State.IsTracking);

        if (_tracking.State.LastCommandedPosition is { } pos)
        {
            await Clients.Caller.SendAsync("PositionUpdate",
                pos.Altitude.Degrees, pos.Azimuth.Degrees,
                _tracking.State.TargetName, _tracking.State.IsTracking);
        }

        await base.OnConnectedAsync();
    }

    public async Task SendGCode(string gcode)
    {
        await _gcode.SendRawAsync(gcode);
    }

    public async Task Goto(string targetKey)
    {
        if (targetKey == "Sun")
        {
            _tracking.SetTarget(Catalog.Sun, "Sun");
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
    }

    public async Task GotoCustom(string ra, string dec)
    {
        var coord = EquatorialCoordinate.FromRaDec(ra, dec);
        _tracking.SetTarget(_ => coord, $"Custom ({ra}, {dec})");
        await _tracking.GotoAsync();
    }

    public async Task StartTracking()
    {
        _tracking.StartTracking();
        await Clients.All.SendAsync("TrackingStatus", true);
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
    }

    public void AdoptPosition()
    {
        _tracking.AdoptPosition();
    }

    public async Task CalibrateRaDec(double ra, double dec)
    {
        var coord = new EquatorialCoordinate(
            Angle.FromDegrees(ra),
            Angle.FromDegrees(dec));

        var now = DateTimeOffset.UtcNow;
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

        var now = DateTimeOffset.UtcNow;
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
        await Clients.All.SendAsync("ConnectionStatus", _serial.IsConnected, _serial.CurrentPort);
    }

    public async Task Disconnect()
    {
        _tracking.StopTracking();
        await _serial.DisconnectAsync();
        await Clients.All.SendAsync("ConnectionStatus", false, null);
        await Clients.All.SendAsync("TrackingStatus", false);
    }

    public async Task SetObserverLocation(double lat, double lon)
    {
        _tracking.SetObserverLocation(lat, lon);
        await Clients.All.SendAsync("ObserverLocationSet", lat, lon);
    }

    public Task GetAvailablePorts()
    {
        return Clients.Caller.SendAsync("AvailablePorts", _serial.AvailablePorts);
    }
}
