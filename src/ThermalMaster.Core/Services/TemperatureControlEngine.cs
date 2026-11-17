using ThermalMaster.Core.Enums;
using ThermalMaster.Core.Interfaces;
using ThermalMaster.Core.Models;

namespace ThermalMaster.Core.Services;

/// <summary>
/// Motor de control por umbrales directos (según especificación solicitada):
/// - T < 18°C: FRÍO -> Ventilador OFF, Lámpara ON.
/// - 18°C <= T <= 28°C: NORMAL -> ambos OFF.
/// - 28°C < T < 36°C: CALOR -> Ventilador ON, Lámpara OFF.
/// - T >= 36°C: ALARMA -> Ventilador ON, Lámpara OFF.
/// En modo manual permite control individual de relés. Se mantiene exclusión mutua de seguridad.
/// </summary>
public class TemperatureControlEngine : ITemperatureControlEngine
{
    private readonly object _lock = new();

    public OperatingMode CurrentMode { get; private set; } = OperatingMode.Automatic;
    public ClimateControlState CurrentState { get; private set; } = ClimateControlState.Normal;
    public FanState DesiredFanState { get; private set; } = FanState.Off;
    public LampState DesiredLampState { get; private set; } = LampState.Off;
    public SystemConfiguration Configuration { get; private set; }

    public TemperatureControlEngine(SystemConfiguration? initialConfig = null)
    {
        Configuration = initialConfig ?? new SystemConfiguration();
    }

    public void SetMode(OperatingMode mode)
    {
        lock (_lock)
        {
            CurrentMode = mode;
            if (mode == OperatingMode.Automatic)
            {
                // En automático los actuadores se determinan por la temperatura evaluada.
                // Se reinicia a estado seguro neutro hasta la próxima evaluación.
                CurrentState = ClimateControlState.Normal;
                DesiredFanState = FanState.Off;
                DesiredLampState = LampState.Off;
            }
        }
    }

    public void SetManualFanState(FanState state)
    {
        lock (_lock)
        {
            if (CurrentMode == OperatingMode.Manual)
            {
                DesiredFanState = state;
            }
        }
    }

    public void SetManualLampState(LampState state)
    {
        lock (_lock)
        {
            if (CurrentMode == OperatingMode.Manual)
            {
                DesiredLampState = state;
            }
        }
    }

    public void UpdateConfiguration(SystemConfiguration configuration)
    {
        lock (_lock)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
    }

    /// <summary>
    /// Evalúa la temperatura actual y determina estado/actuadores según los umbrales definidos.
    /// </summary>
    public (FanState fan, LampState lamp) EvaluateEnvironment(double temperatureC, double humidityPct)
    {
        lock (_lock)
        {
            if (CurrentMode == OperatingMode.Manual)
            {
                ApplySafetyInterlock();
                return (DesiredFanState, DesiredLampState);
            }

            double min = Configuration.MinTemperatureThreshold;   // 18.0
            double max = Configuration.MaxTemperatureThreshold;   // 28.0
            double alarm = Configuration.AlarmTemperatureThreshold > 0 ? Configuration.AlarmTemperatureThreshold : 36.0;

            if (double.IsNaN(temperatureC) || double.IsInfinity(temperatureC))
            {
                // Protección ante lectura inválida: no accionar actuadores
                CurrentState = ClimateControlState.Normal;
                DesiredFanState = FanState.Off;
                DesiredLampState = LampState.Off;
                return (DesiredFanState, DesiredLampState);
            }

            if (temperatureC < min)
            {
                CurrentState = ClimateControlState.Cold;
                DesiredFanState = FanState.Off;
                DesiredLampState = LampState.On;
            }
            else if (temperatureC <= max)
            {
                CurrentState = ClimateControlState.Normal;
                DesiredFanState = FanState.Off;
                DesiredLampState = LampState.Off;
            }
            else if (temperatureC < alarm)
            {
                CurrentState = ClimateControlState.Heat;
                DesiredFanState = FanState.On;
                DesiredLampState = LampState.Off;
            }
            else // temperatureC >= alarm
            {
                CurrentState = ClimateControlState.Alarm;
                DesiredFanState = FanState.On;
                DesiredLampState = LampState.Off;
            }

            ApplySafetyInterlock();
            return (DesiredFanState, DesiredLampState);
        }
    }

    private void ApplySafetyInterlock()
    {
        // Exclusión mutua de seguridad
        if (DesiredFanState == FanState.On && DesiredLampState == LampState.On)
        {
            DesiredFanState = FanState.Off;
            DesiredLampState = LampState.Off;
        }
    }
}
