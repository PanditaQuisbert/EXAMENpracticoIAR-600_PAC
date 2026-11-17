using ThermalMaster.Core.DTOs;

namespace ThermalMaster.Web.Hubs;

/// <summary>
/// Contrato de métodos que el servidor SignalR invoca en los clientes web (Frontend).
/// </summary>
public interface ITelemetryHubClient
{
    Task ReceiveTelemetry(TelemetryDto telemetry);
    Task ReceiveConnectionStatus(string status, string? portName);
    Task ReceiveAlert(string message, string severity);
    Task ReceiveCommandAck(string commandType, bool success, string message);
}
