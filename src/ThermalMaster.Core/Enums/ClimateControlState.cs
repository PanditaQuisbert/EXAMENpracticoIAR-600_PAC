namespace ThermalMaster.Core.Enums;

/// <summary>
/// Estados térmicos/operativos del control de climatización.
/// Reglas: T &lt; 18 => FRÍO (lámpara ON), 18 &lt;= T &lt;= 28 => NORMAL (ambos OFF),
/// 28 &lt; T &lt; 36 => CALOR (ventilador ON), T &gt;= 36 => ALARMA (ventilador ON).
/// </summary>
public enum ClimateControlState
{
    /// <summary>NORMAL (18.0°C &lt;= T &lt;= 28.0°C): ambos actuadores apagados.</summary>
    Normal = 0,
    /// <summary>FRÍO (T &lt; 18.0°C): lámpara encendida, ventilador apagado.</summary>
    Cold = 1,
    /// <summary>CALOR (28.0°C &lt; T &lt; 36.0°C): ventilador encendido, lámpara apagada.</summary>
    Heat = 2,
    /// <summary>ALARMA (T &gt;= 36.0°C): ventilador encendido, lámpara apagada.</summary>
    Alarm = 3,

    // Alias de compatibilidad
    ComfortIdle = Normal,
    HeatingActive = Cold,
    CoolingActive = Heat
}
