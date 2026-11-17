using ThermalMaster.Core.Models;

namespace ThermalMaster.Core.Interfaces;

/// <summary>
/// Contrato de persistencia para el registro de alarmas y eventos en SQL Server.
/// </summary>
public interface IAlertRepository
{
    Task<long> LogAlertAsync(AlertLog alert, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertLog>> GetRecentAlertsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task ResolveAlertAsync(long alertId, CancellationToken cancellationToken = default);
}
