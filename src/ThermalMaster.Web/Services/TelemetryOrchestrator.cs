using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using ThermalMaster.Core.DTOs;
using ThermalMaster.Core.Enums;
using ThermalMaster.Core.Interfaces;
using ThermalMaster.Core.Models;
using ThermalMaster.Web.Hubs;

namespace ThermalMaster.Web.Services;

public class TelemetryOrchestrator : IHostedService, IDisposable
{
    private readonly ISerialPortManager _serial;
    private readonly ITelemetryRepository _telemetryRepo;
    private readonly IAlertRepository _alertRepo;
    private readonly ICommandRepository _commandRepo;
    private readonly ITemperatureControlEngine _engine;
    private readonly IHubContext<TelemetryHub, ITelemetryHubClient> _hub;
    private readonly ILogger<TelemetryOrchestrator> _logger;

    public TelemetryOrchestrator(
        ISerialPortManager serial,
        ITelemetryRepository telemetryRepo,
        IAlertRepository alertRepo,
        ICommandRepository commandRepo,
        ITemperatureControlEngine engine,
        IHubContext<TelemetryHub, ITelemetryHubClient> hub,
        ILogger<TelemetryOrchestrator> logger)
    {
        _serial = serial;
        _telemetryRepo = telemetryRepo;
        _alertRepo = alertRepo;
        _commandRepo = commandRepo;
        _engine = engine;
        _hub = hub;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _serial.TelemetryReceived += OnTelemetryReceived;
        _serial.ConnectionStateChanged += OnConnectionStateChanged;
        _serial.RawDataReceived += OnRawDataReceived;
        _serial.RawDataSent += OnRawDataSent;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Inyecta telemetría desde el navegador (simulación explícita del operador).
    /// Calcula actuadores con el motor de control y reutiliza el pipeline de visualización/persistencia.
    /// No sustituye ni simula de forma oculta la comunicación con Arduino/Proteus.
    /// </summary>
    public async Task<TelemetryDto> InjectBrowserTelemetryAsync(double temperatureC, double humidityPct, CancellationToken cancellationToken = default)
    {
        if (double.IsNaN(temperatureC) || double.IsInfinity(temperatureC))
            throw new ArgumentOutOfRangeException(nameof(temperatureC), "Temperatura inválida.");
        if (double.IsNaN(humidityPct) || double.IsInfinity(humidityPct))
            humidityPct = 0;
        humidityPct = Math.Clamp(humidityPct, 0, 100);

        var (fan, lamp) = _engine.EvaluateEnvironment(temperatureC, humidityPct);
        var record = new TelemetryRecord
        {
            Timestamp = DateTime.UtcNow,
            TemperatureC = Math.Round(temperatureC, 2),
            HumidityPct = Math.Round(humidityPct, 2),
            FanStatus = fan,
            LampStatus = lamp,
            Mode = _engine.CurrentMode,
            MinThreshold = _engine.Configuration.MinTemperatureThreshold,
            MaxThreshold = _engine.Configuration.MaxTemperatureThreshold,
            ProteusAcknowledged = false
        };

        return await ProcessTelemetryCoreAsync(record, "browser", cancellationToken);
    }

    private void Unsubscribe()
    {
        _serial.TelemetryReceived -= OnTelemetryReceived;
        _serial.ConnectionStateChanged -= OnConnectionStateChanged;
        _serial.RawDataReceived -= OnRawDataReceived;
        _serial.RawDataSent -= OnRawDataSent;
    }

    private async void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        try
        {
            await _hub.Clients.All.ReceiveConnectionStatus(state.ToString(), _serial.CurrentPort);
            if (state == ConnectionState.Error || state == ConnectionState.Disconnected)
            {
                await _hub.Clients.All.ReceiveAlert($"Estado de conexión serial: {state}", state == ConnectionState.Error ? "Warning" : "Normal");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error notificando cambio de estado de conexión");
        }
    }

    private async void OnTelemetryReceived(object? sender, TelemetryRecord record)
    {
        try
        {
            // Mantener modo del motor coherente con la telemetría recibida desde el dispositivo
            _engine.SetMode(record.Mode);
            await ProcessTelemetryCoreAsync(record, "serial");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error procesando telemetría recibida");
        }
    }

    private async Task<TelemetryDto> ProcessTelemetryCoreAsync(TelemetryRecord record, string source, CancellationToken cancellationToken = default)
    {
        // Mantener umbrales coherentes con el motor
        var cfg = _engine.Configuration;
        record.MinThreshold = cfg.MinTemperatureThreshold;
        record.MaxThreshold = cfg.MaxTemperatureThreshold;

        // Evaluación de severidad/estado según reglas: <18 FRÍO, 18-28 NORMAL, >28 y <36 CALOR, >=36 ALARMA
        var alarmThreshold = _engine.Configuration.AlarmTemperatureThreshold > 0 ? _engine.Configuration.AlarmTemperatureThreshold : 36.0;
        if (record.TemperatureC < record.MinThreshold)
            record.Severity = AlertSeverity.Warning;      // FRÍO
        else if (record.TemperatureC <= record.MaxThreshold)
            record.Severity = AlertSeverity.Normal;       // NORMAL
        else if (record.TemperatureC < alarmThreshold)
            record.Severity = AlertSeverity.Warning;      // CALOR
        else
            record.Severity = AlertSeverity.Critical;     // ALARMA

        // Asegurar coherencia de modo reportado
        record.Mode = _engine.CurrentMode;

        var dto = new TelemetryDto
        {
            Timestamp = record.Timestamp,
            TemperatureC = record.TemperatureC,
            HumidityPct = record.HumidityPct,
            FanStatus = record.FanStatus == FanState.On ? "On" : "Off",
            LampStatus = record.LampStatus == LampState.On ? "On" : "Off",
            Mode = record.Mode == OperatingMode.Manual ? "Manual" : "Automatic",
            Severity = record.Severity.ToString(),
            MinThreshold = record.MinThreshold,
            MaxThreshold = record.MaxThreshold,
            IsConnected = _serial.State == ConnectionState.Connected
        };

        await _hub.Clients.All.ReceiveTelemetry(dto);

        if (source == "browser")
        {
            await _hub.Clients.All.ReceiveAlert($"Simulación (navegador): {record.TemperatureC:F1}°C / {record.HumidityPct:F1}% HR", "Normal");
        }

        try { await _telemetryRepo.InsertTelemetryAsync(record, cancellationToken); }
        catch (Exception ex) { _logger.LogWarning(ex, "No se pudo persistir telemetría en SQL Server (origen: {Source})", source); }

        if (record.Severity != AlertSeverity.Normal)
        {
            string stateLabel;
            if (record.TemperatureC < record.MinThreshold) stateLabel = "FRÍO";
            else if (record.TemperatureC <= record.MaxThreshold) stateLabel = "NORMAL";
            else if (record.TemperatureC < alarmThreshold) stateLabel = "CALOR";
            else stateLabel = "ALARMA";

            var msg = record.Severity == AlertSeverity.Critical
                ? $"ALARMA de temperatura: {record.TemperatureC:F1}°C (>= {alarmThreshold:F1}°C)"
                : stateLabel == "FRÍO"
                    ? $"Temperatura en FRÍO: {record.TemperatureC:F1}°C (< {record.MinThreshold:F1}°C)"
                    : $"Temperatura en CALOR: {record.TemperatureC:F1}°C (> {record.MaxThreshold:F1}°C)";
            if (source == "browser") msg = "[Simulación] " + msg;
            try
            {
                await _alertRepo.LogAlertAsync(new AlertLog
                {
                    Timestamp = record.Timestamp,
                    Severity = record.Severity,
                    Message = msg,
                    TriggerTemperature = record.TemperatureC,
                    TriggerHumidity = record.HumidityPct,
                    IsResolved = false
                }, cancellationToken);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "No se pudo registrar alerta en SQL Server"); }

            await _hub.Clients.All.ReceiveAlert(msg, record.Severity.ToString());
        }

        return dto;
    }

    private async void OnRawDataReceived(object? sender, string raw)
    {
        try
        {
            // Detectar ACKs y reenviar a clientes
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("ack", out var ackEl))
            {
                var cmdType = ackEl.GetString() ?? "ACK";
                var ok = true;
                if (root.TryGetProperty("status", out var st))
                {
                    var s = st.GetString();
                    ok = string.IsNullOrEmpty(s) || s.Equals("OK", StringComparison.OrdinalIgnoreCase) || s.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
                string msg = $"ACK recibido: {raw}";
                await _hub.Clients.All.ReceiveCommandAck(cmdType, ok, msg);

                // No tenemos correlación exacta de CommandId desde Arduino; se registra confirmación genérica si aplica
                _logger.LogInformation("ACK recibido del dispositivo: {Raw}", raw);
            }
            else if (root.TryGetProperty("pong", out _))
            {
                await _hub.Clients.All.ReceiveCommandAck("PING", true, "PONG recibido");
            }
            else if (root.TryGetProperty("error", out var err))
            {
                await _hub.Clients.All.ReceiveAlert($"Error reportado por dispositivo: {err}", "Warning");
            }
        }
        catch
        {
            // No es JSON válido o no es ACK; ignorar
        }
    }

    private async void OnRawDataSent(object? sender, string raw)
    {
        try
        {
            // Informativo: puede usarse para diagnóstico en clientes si se desea en el futuro
            _logger.LogDebug("TX serial: {Raw}", raw);
            await Task.CompletedTask;
        }
        catch { }
    }

    public void Dispose() => Unsubscribe();
}
