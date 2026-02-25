namespace TelescopeDrive.Services;

public interface ISerialPortService
{
    bool IsConnected { get; }
    string? CurrentPort { get; }
    IReadOnlyList<string> AvailablePorts { get; }
    Task ConnectAsync(string portName, int baudRate);
    Task DisconnectAsync();
    Task<string?> SendLineAsync(string command);
    event Action<string> LineSent;
    event Action<string> LineReceived;
}
