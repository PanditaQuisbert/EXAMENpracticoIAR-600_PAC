namespace ThermalMaster.Core.Enums;

/// <summary>
/// Define el modo operativo del sistema de control térmico.
/// </summary>
public enum OperatingMode
{
    /// <summary>
    /// Modo manual: El usuario decide directamente el encendido o apagado del ventilador.
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Modo automático: El microcontrolador/sistema decide en base a la temperatura e histéresis.
    /// </summary>
    Automatic = 1
}
