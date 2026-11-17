namespace ThermalMaster.Core.Models;

/// <summary>
/// Parámetros de configuración del sistema de monitoreo y control térmico/ambiental.
/// Reglas automáticas: T &lt; Min => FRÍO (lámpara ON), Min &lt;= T &lt;= Max => NORMAL,
/// Max &lt; T &lt; Alarm => CALOR (ventilador ON), T &gt;= Alarm => ALARMA (ventilador ON).
/// </summary>
public class SystemConfiguration
{
    public int Id { get; set; } = 1;
    public string SerialPortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public double MinTemperatureThreshold { get; set; } = 18.0; // Límite inferior de zona NORMAL
    public double MaxTemperatureThreshold { get; set; } = 28.0; // Límite superior de zona NORMAL
    public double HysteresisOffset { get; set; } = 1.0;          // Conservado por compatibilidad (no usado en control por umbrales directos)
    public double AlarmTemperatureThreshold { get; set; } = 36.0; // &gt;= 36°C: ALARMA
    public int TelemetryIntervalMs { get; set; } = 1000;
    public bool EnableAudibleAlert { get; set; } = true;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
