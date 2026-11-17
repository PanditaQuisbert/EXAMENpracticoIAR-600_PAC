using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using ThermalMaster.Core.DTOs;
using ThermalMaster.Core.Enums;
using ThermalMaster.Core.Interfaces;
using ThermalMaster.Core.Models;
using ThermalMaster.Core.Services;
using ThermalMaster.Infrastructure.Data;
using ThermalMaster.Infrastructure.Serial;
using ThermalMaster.Web.Hubs;
using ThermalMaster.Web.Services;

// Resolver dinámicamente la ruta de ContentRoot / WebRoot para permitir ejecución desde la raíz o desde Visual Studio
var contentRoot = Directory.GetCurrentDirectory();
var candidateWebRoot = Path.Combine(contentRoot, "wwwroot");
if (!Directory.Exists(candidateWebRoot))
{
    var nestedPath = Path.Combine(contentRoot, "src", "ThermalMaster.Web");
    if (Directory.Exists(Path.Combine(nestedPath, "wwwroot")))
    {
        contentRoot = nestedPath;
    }
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});

// Servicios del sistema
builder.Services.AddSignalR();

var connectionString = builder.Configuration.GetConnectionString("ThermalMasterDb")
    ?? "Server=localhost;Database=ThermalMasterDB;Integrated Security=True;TrustServerCertificate=True;";
builder.Services.AddSingleton(new DatabaseConnectionFactory(connectionString));
builder.Services.AddSingleton<ITelemetryRepository, TelemetryRepository>();
builder.Services.AddSingleton<IAlertRepository, AlertRepository>();
builder.Services.AddSingleton<ICommandRepository, CommandRepository>();

builder.Services.AddSingleton<ISerialPortManager, SerialPortManager>();
builder.Services.AddSingleton<ITemperatureControlEngine>(sp =>
{
    var config = builder.Configuration;
    var min = ParseDouble(config["ThermalLimits:MinTempC"], 18.0);
    var max = ParseDouble(config["ThermalLimits:MaxTempC"], 28.0);
    var hyst = ParseDouble(config["ThermalLimits:HysteresisC"], 1.0);
    var interval = ParseInt(config["ThermalLimits:PollingIntervalMs"], 1000);
    var port = config["Hardware:DefaultPort"] ?? "COM1";
    var baud = ParseInt(config["Hardware:DefaultBaudRate"], 9600);
    return new TemperatureControlEngine(new SystemConfiguration
    {
        SerialPortName = port,
        BaudRate = baud,
        MinTemperatureThreshold = min,
        MaxTemperatureThreshold = max,
        HysteresisOffset = hyst,
        TelemetryIntervalMs = interval
    });
});

builder.Services.AddSingleton<TelemetryOrchestrator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TelemetryOrchestrator>());

// CORS para habilitar desarrollo y pruebas locales
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// Mapeo del Hub SignalR para streaming en tiempo real
app.MapHub<TelemetryHub>("/hubs/telemetry");

// Estado y configuración
app.MapGet("/api/status", (ISerialPortManager serial, IConfiguration config, ITemperatureControlEngine engine) => Results.Ok(new
{
    System = "ThermalMaster SCADA",
    SerialState = serial.State.ToString(),
    CurrentPort = serial.CurrentPort,
    BaudRate = serial.CurrentBaudRate,
    IsConnected = serial.State == ConnectionState.Connected,
    Mode = engine.CurrentMode.ToString(),
    EngineState = engine.CurrentState.ToString(),
    Limits = new
    {
        MinTemp = engine.Configuration.MinTemperatureThreshold,
        MaxTemp = engine.Configuration.MaxTemperatureThreshold,
        Hysteresis = engine.Configuration.HysteresisOffset
    },
    Sensor = "DHT22 (Pin D2)",
    Actuators = new { FanRelay = "D7", LampRelay = "D8" },
    DatabaseTarget = "SQL Server",
    Timestamp = DateTime.UtcNow
}));

app.MapGet("/api/ports", (ISerialPortManager serial) => Results.Ok(new
{
    AvailablePorts = serial.GetAvailablePorts()
}));

app.MapGet("/api/config", (IConfiguration config, ITemperatureControlEngine engine) => Results.Ok(new
{
    Port = engine.Configuration.SerialPortName,
    BaudRate = engine.Configuration.BaudRate,
    MinTemp = engine.Configuration.MinTemperatureThreshold,
    MaxTemp = engine.Configuration.MaxTemperatureThreshold,
    Hysteresis = engine.Configuration.HysteresisOffset,
    TelemetryIntervalMs = engine.Configuration.TelemetryIntervalMs
}));

// Conexión serial
app.MapPost("/api/serial/connect", async (HttpRequest request, ISerialPortManager serial, IConfiguration config, IHubContext<TelemetryHub, ITelemetryHubClient> hub) =>
{
    string? port = null;
    int? baud = null;
    try
    {
        using var doc = await JsonDocument.ParseAsync(request.Body);
        if (doc.RootElement.TryGetProperty("port", out var p)) port = p.GetString();
        if (doc.RootElement.TryGetProperty("baudRate", out var b) && b.TryGetInt32(out var bv)) baud = bv;
    }
    catch { /* cuerpo vacío o no JSON: usar defaults */ }

    port ??= config["Hardware:DefaultPort"] ?? "COM1";
    baud ??= ParseInt(config["Hardware:DefaultBaudRate"], 9600);

    var ok = await serial.ConnectAsync(port, baud.Value);
    await hub.Clients.All.ReceiveConnectionStatus(serial.State.ToString(), serial.CurrentPort);
    return Results.Ok(new { success = ok, state = serial.State.ToString(), port = serial.CurrentPort, baudRate = serial.CurrentBaudRate });
});

app.MapPost("/api/serial/disconnect", async (ISerialPortManager serial, IHubContext<TelemetryHub, ITelemetryHubClient> hub) =>
{
    await serial.DisconnectAsync();
    await hub.Clients.All.ReceiveConnectionStatus(serial.State.ToString(), serial.CurrentPort);
    return Results.Ok(new { success = true, state = serial.State.ToString() });
});

// Datos para dashboard
app.MapGet("/api/telemetry/latest", async (ITelemetryRepository repo) =>
{
    try
    {
        var latest = await repo.GetLatestReadingAsync();
        return latest is null ? Results.NotFound(new { message = "Sin lecturas registradas" }) : Results.Ok(latest);
    }
    catch (Exception ex)
    {
        return Results.Problem($"No se pudo consultar telemetría: {ex.Message}");
    }
});

app.MapGet("/api/telemetry/history", async (ITelemetryRepository repo, int? limit) =>
{
    try
    {
        var to = DateTime.UtcNow;
        var from = to.AddHours(-6);
        var items = await repo.GetHistoricalReadingsAsync(from, to, limit ?? 200);
        return Results.Ok(new { items });
    }
    catch (Exception ex)
    {
        return Results.Problem($"No se pudo consultar historial: {ex.Message}");
    }
});

app.MapGet("/api/alerts/recent", async (IAlertRepository repo, int? limit) =>
{
    try
    {
        var items = await repo.GetRecentAlertsAsync(limit ?? 50);
        return Results.Ok(new { items });
    }
    catch (Exception ex)
    {
        return Results.Problem($"No se pudo consultar alertas: {ex.Message}");
    }
});

// Simulación explícita desde navegador (no reemplaza telemetría real de Proteus/Arduino)
app.MapPost("/api/simulation/telemetry", async (HttpRequest request, TelemetryOrchestrator orchestrator) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(request.Body);
        var root = doc.RootElement;
        if (!root.TryGetProperty("temperatureC", out var tEl) || !tEl.TryGetDouble(out var temp))
            return Results.BadRequest(new { success = false, message = "temperatureC es requerido (número)." });
        double hum = 50.0;
        if (root.TryGetProperty("humidityPct", out var hEl) && hEl.TryGetDouble(out var hv)) hum = hv;
        var dto = await orchestrator.InjectBrowserTelemetryAsync(temp, hum);
        return Results.Ok(new { success = true, source = "browser", telemetry = dto });
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem($"No se pudo procesar simulación: {ex.Message}");
    }
});

app.Run();

static double ParseDouble(string? value, double fallback)
{
    if (string.IsNullOrWhiteSpace(value)) return fallback;
    if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
    if (double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out d)) return d;
    return fallback;
}

static int ParseInt(string? value, int fallback)
{
    if (string.IsNullOrWhiteSpace(value)) return fallback;
    if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var i)) return i;
    if (int.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out i)) return i;
    return fallback;
}

