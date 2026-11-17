namespace ThermalMaster.Core.DTOs;

public class CommandRequestDto
{
    public string CommandType { get; set; } = string.Empty;
    public string? Parameter { get; set; }
}
