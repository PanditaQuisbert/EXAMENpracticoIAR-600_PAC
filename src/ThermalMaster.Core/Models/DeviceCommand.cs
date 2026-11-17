namespace ThermalMaster.Core.Models;

/// <summary>
/// Representa una orden de control enviada desde la interfaz web hacia el Arduino.
/// </summary>
public class DeviceCommand
{
    public Guid CommandId { get; set; } = Guid.NewGuid();
    public string CommandType { get; set; } = string.Empty; // "FAN_ON", "FAN_OFF", "SET_MODE_AUTO", "SET_MODE_MANUAL", "SET_LIMITS", "PING"
    public string? Parameter { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public bool AcknowledgedByArduino { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}
