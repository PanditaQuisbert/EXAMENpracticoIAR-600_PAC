namespace ThermalMaster.Core.DTOs;

public class SystemSettingsDto
{
    public string SerialPortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public double MinTemperatureThreshold { get; set; } = 18.0;
    public double MaxTemperatureThreshold { get; set; } = 28.0;
    public double HysteresisOffset { get; set; } = 1.0;
    public int TelemetryIntervalMs { get; set; } = 1000;
    public bool EnableAudibleAlert { get; set; } = true;
}
