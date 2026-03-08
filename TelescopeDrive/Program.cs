using Microsoft.AspNetCore.SignalR;
using TelescopeDrive.Hubs;
using TelescopeDrive.Models;
using TelescopeDrive.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ObserverConfig>(
    builder.Configuration.GetSection("Observer"));

var serialPort = builder.Configuration.GetValue<string>("Observer:SerialPort") ?? "";
if (serialPort.Equals("simulated", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<ISerialPortService, SimulatedSerialPortService>();
else
    builder.Services.AddSingleton<ISerialPortService, SerialPortService>();
builder.Services.AddSingleton<ClockService>();
builder.Services.AddSingleton<IClock>(sp => sp.GetRequiredService<ClockService>());
builder.Services.AddSingleton<IGCodeService, GCodeService>();
builder.Services.AddSingleton<ISolverService, WatneySolverService>();
builder.Services.AddSingleton<ITrackingService, TrackingService>();
builder.Services.AddSingleton<IAlignmentModel, AlignmentModel>();
builder.Services.AddHostedService<TrackingBackgroundService>();

builder.Services.AddRazorPages();
builder.Services.AddSignalR();

var app = builder.Build();

// Wire serial port events to SignalR hub
var serial = app.Services.GetRequiredService<ISerialPortService>();
var hubContext = app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<TelescopeHub>>();

serial.LineSent += line =>
    hubContext.Clients.All.SendAsync("GCodeSent", DateTimeOffset.Now.ToString("HH:mm:ss.fff"), line);
serial.LineReceived += line =>
    hubContext.Clients.All.SendAsync("GCodeReceived", DateTimeOffset.Now.ToString("HH:mm:ss.fff"), line);

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapHub<TelescopeHub>("/hubs/telescope");

app.Run();
