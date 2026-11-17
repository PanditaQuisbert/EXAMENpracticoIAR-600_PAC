using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using ThermalMaster.Core.DTOs;
using ThermalMaster.Core.Enums;
using ThermalMaster.Core.Interfaces;
using ThermalMaster.Core.Models;

namespace ThermalMaster.Web.Hubs;

/// <summary>
/// Hub SignalR central para la comunicación bidireccional en tiempo real entre el navegador y el backend.
/// </summary>
public class TelemetryHub : Hub<ITelemetryHubClient>
{
    private readonly ISerialPortManager _serialManager;
    private readonly ITemperatureControlEngine _engine;
    private readonly ICommandRepository _commandRepository;
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(
        ISerialPortManager serialManager,
        ITemperatureControlEngine engine,
        ICommandRepository commandRepository,
        ILogger<TelemetryHub> logger)
    {
        _serialManager = serialManager;
        _engine = engine;
        _commandRepository = commandRepository;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Cliente conectado al Hub de telemetría: {ConnectionId}", Context.ConnectionId);

        await Clients.Caller.ReceiveConnectionStatus(
            _serialManager.State.ToString(),
            _serialManager.CurrentPort
        );

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Cliente desconectado del Hub: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Recibe una orden de control desde la interfaz web y la envía al Arduino cuando hay conexión serial.
    /// </summary>
    public async Task SendControlCommand(CommandRequestDto request)
    {
        var cmdType = (request.CommandType ?? string.Empty).Trim().ToUpperInvariant();
        _logger.LogInformation("Comando recibido desde cliente {ConnectionId}: {CommandType} ({Parameter})",
            Context.ConnectionId, cmdType, request.Parameter);

        var command = new DeviceCommand
        {
            CommandId = Guid.NewGuid(),
            CommandType = cmdType,
            Parameter = request.Parameter,
            IssuedAt = DateTime.UtcNow,
            AcknowledgedByArduino = false
        };

        // Actualizar estado local del motor/configuración según el comando solicitado
        ApplyLocalState(cmdType, request.Parameter);

        if (_serialManager.State != ConnectionState.Connected)
        {
            try { await _commandRepository.InsertCommandAsync(command); } catch { /* no bloquear UI por BD */ }
            await Clients.Caller.ReceiveCommandAck(
                cmdType,
                false,
                "No se pudo ejecutar el comando: el puerto serial con Arduino/Proteus está desconectado."
            );
            return;
        }

        try { await _commandRepository.InsertCommandAsync(command); } catch (Exception ex) { _logger.LogWarning(ex, "No se pudo registrar comando en BD"); }

        var sent = await _serialManager.SendCommandAsync(command);

        await Clients.Caller.ReceiveCommandAck(
            cmdType,
            sent,
            sent ? "Comando enviado al microcontrolador (esperando ACK)." : "Error al transmitir por puerto serial."
        );
    }

    private void ApplyLocalState(string cmdType, string? parameter)
    {
        switch (cmdType)
        {
            case "SET_MODE":
            {
                var mode = (parameter ?? "AUTO").Trim().ToUpperInvariant();
                _engine.SetMode(mode.StartsWith("MANUAL") ? OperatingMode.Manual : OperatingMode.Automatic);
                break;
            }
            case "SET_MODE_AUTO":
                _engine.SetMode(OperatingMode.Automatic);
                break;
            case "SET_MODE_MANUAL":
                _engine.SetMode(OperatingMode.Manual);
                break;
            case "FAN_ON":
                _engine.SetManualFanState(FanState.On);
                break;
            case "FAN_OFF":
                _engine.SetManualFanState(FanState.Off);
                break;
            case "LAMP_ON":
                _engine.SetManualLampState(LampState.On);
                break;
            case "LAMP_OFF":
                _engine.SetManualLampState(LampState.Off);
                break;
            case "SET_LIMITS":
                if (TryParseLimits(parameter, out var min, out var max))
                {
                    var cfg = _engine.Configuration;
                    // conservar histéresis y demás parámetros existentes
                    _engine.UpdateConfiguration(new SystemConfiguration
                    {
                        Id = cfg.Id,
                        SerialPortName = cfg.SerialPortName,
                        BaudRate = cfg.BaudRate,
                        MinTemperatureThreshold = min,
                        MaxTemperatureThreshold = max,
                        HysteresisOffset = cfg.HysteresisOffset,
                        TelemetryIntervalMs = cfg.TelemetryIntervalMs,
                        EnableAudibleAlert = cfg.EnableAudibleAlert,
                        LastUpdated = DateTime.UtcNow
                    });
                }
                break;
        }
    }

    private static bool TryParseLimits(string? parameter, out double min, out double max)
    {
        min = 18.0; max = 28.0;
        if (string.IsNullOrWhiteSpace(parameter)) return false;

        var parts = parameter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2 &&
            double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out min) &&
            double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out max))
        {
            return min < max;
        }

        // tolerar coma decimal o formatos mixtos
        var nums = Regex.Matches(parameter, @"-?\d+(?:[\.,]\d+)?");
        if (nums.Count >= 2)
        {
            if (double.TryParse(nums[0].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out min) &&
                double.TryParse(nums[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out max))
            {
                return min < max;
            }
        }
        return false;
    }
}

