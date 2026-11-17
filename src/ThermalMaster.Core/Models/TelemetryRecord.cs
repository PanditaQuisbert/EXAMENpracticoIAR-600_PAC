using ThermalMaster.Core.Enums;

namespace ThermalMaster.Core.Models;

/// <summary>
/// Representa una lectura instantánea de telemetría proveniente del sensor DHT22 y el Arduino simulado en Proteus.
/// </summary>
public class TelemetryRecord
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double TemperatureC { get; set; }
    public double HumidityPct { get; set; }
    public FanState FanStatus { get; set; }
    public LampState LampStatus { get; set; }
    public OperatingMode Mode { get; set; }
    public AlertSeverity Severity { get; set; }
    public double MinThreshold { get; set; }
    public double MaxThreshold { get; set; }
    public bool ProteusAcknowledged { get; set; }
}
