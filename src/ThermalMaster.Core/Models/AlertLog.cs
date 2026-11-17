using ThermalMaster.Core.Enums;

namespace ThermalMaster.Core.Models;

/// <summary>
/// Registro histórico de eventos y alarmas térmicas.
/// </summary>
public class AlertLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public double TriggerTemperature { get; set; }
    public double? TriggerHumidity { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
