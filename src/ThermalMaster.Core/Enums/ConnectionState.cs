namespace ThermalMaster.Core.Enums;

/// <summary>
/// Estado de la conexión física o virtual con el puerto COM.
/// </summary>
public enum ConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Error = 3
}
