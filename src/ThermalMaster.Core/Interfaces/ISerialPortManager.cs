using ThermalMaster.Core.Enums;
using ThermalMaster.Core.Models;

namespace ThermalMaster.Core.Interfaces;

/// <summary>
/// Contrato para el servicio de comunicación serial con Arduino (físico o Proteus COMPIM).
/// </summary>
public interface ISerialPortManager
{
    ConnectionState State { get; }
    string CurrentPort { get; }
    int CurrentBaudRate { get; }

    event EventHandler<TelemetryRecord>? TelemetryReceived;
    event EventHandler<ConnectionState>? ConnectionStateChanged;
    event EventHandler<string>? RawDataReceived;
    event EventHandler<string>? RawDataSent;

    Task<bool> ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<bool> SendCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default);
    string[] GetAvailablePorts();
}
