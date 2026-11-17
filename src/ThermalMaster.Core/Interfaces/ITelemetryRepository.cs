using ThermalMaster.Core.Models;

namespace ThermalMaster.Core.Interfaces;

/// <summary>
/// Contrato de persistencia para el almacenamiento y consulta de lecturas de telemetría en SQL Server.
/// </summary>
public interface ITelemetryRepository
{
    Task<long> InsertTelemetryAsync(TelemetryRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetryRecord>> GetHistoricalReadingsAsync(DateTime from, DateTime to, int limit = 500, CancellationToken cancellationToken = default);
    Task<TelemetryRecord?> GetLatestReadingAsync(CancellationToken cancellationToken = default);
}
