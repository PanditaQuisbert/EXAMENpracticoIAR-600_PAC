using ThermalMaster.Core.Enums;
using ThermalMaster.Core.Models;

namespace ThermalMaster.Core.Interfaces;

/// <summary>
/// Contrato para el motor inteligente de climatización con máquina de estados finita e histéresis.
/// </summary>
public interface ITemperatureControlEngine
{
    OperatingMode CurrentMode { get; }
    ClimateControlState CurrentState { get; }
    FanState DesiredFanState { get; }
    LampState DesiredLampState { get; }
    SystemConfiguration Configuration { get; }

    void SetMode(OperatingMode mode);
    void SetManualFanState(FanState state);
    void SetManualLampState(LampState state);
    void UpdateConfiguration(SystemConfiguration configuration);
    (FanState fan, LampState lamp) EvaluateEnvironment(double temperatureC, double humidityPct);
}
