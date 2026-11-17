namespace ThermalMaster.Core.DTOs;

public class TelemetryDto
{
    public DateTime Timestamp { get; set; }
    public double TemperatureC { get; set; }
    public double HumidityPct { get; set; }
    public string FanStatus { get; set; } = "Off";
    public string LampStatus { get; set; } = "Off";
    public string Mode { get; set; } = "Automatic";
    public string Severity { get; set; } = "Normal";
    public double MinThreshold { get; set; }
    public double MaxThreshold { get; set; }
    public bool IsConnected { get; set; }
}
