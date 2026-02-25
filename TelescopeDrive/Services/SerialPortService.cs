using System.IO.Ports;

namespace TelescopeDrive.Services;

public class SerialPortService : ISerialPortService, IDisposable
{
    private SerialPort? _port;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<SerialPortService> _logger;

    public SerialPortService(ILogger<SerialPortService> logger)
    {
        _logger = logger;
    }

    public bool IsConnected => _port?.IsOpen == true;
    public string? CurrentPort => _port?.PortName;
    public IReadOnlyList<string> AvailablePorts => SerialPort.GetPortNames();

    public event Action<string>? LineSent;
    public event Action<string>? LineReceived;

    public Task ConnectAsync(string portName, int baudRate)
    {
        if (_port?.IsOpen == true)
            _port.Close();

        _port = new SerialPort(portName, baudRate)
        {
            ReadTimeout = 5000,
            WriteTimeout = 5000,
            NewLine = "\n",
            DtrEnable = true,
        };
        _port.DataReceived += OnDataReceived;
        _port.Open();
        _logger.LogInformation("Connected to {Port} at {Baud}", portName, baudRate);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        if (_port?.IsOpen == true)
        {
            _port.DataReceived -= OnDataReceived;
            _port.Close();
            _logger.LogInformation("Disconnected from {Port}", _port.PortName);
        }
        _port?.Dispose();
        _port = null;
        return Task.CompletedTask;
    }

    public async Task<string?> SendLineAsync(string command)
    {
        if (_port?.IsOpen != true)
            return null;

        await _semaphore.WaitAsync();
        try
        {
            _port.WriteLine(command);
            LineSent?.Invoke(command);
            _logger.LogDebug("TX: {Command}", command);

            // Read response (blocking with timeout)
            var response = _port.ReadLine().Trim();
            LineReceived?.Invoke(response);
            _logger.LogDebug("RX: {Response}", response);
            return response;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timeout waiting for response to: {Command}", command);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Serial IO error");
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port?.IsOpen != true) return;
        try
        {
            while (_port.BytesToRead > 0)
            {
                var line = _port.ReadLine().Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    LineReceived?.Invoke(line);
                    _logger.LogDebug("RX (async): {Line}", line);
                }
            }
        }
        catch { /* port may have closed */ }
    }

    public void Dispose()
    {
        _port?.Dispose();
        _semaphore.Dispose();
    }
}
