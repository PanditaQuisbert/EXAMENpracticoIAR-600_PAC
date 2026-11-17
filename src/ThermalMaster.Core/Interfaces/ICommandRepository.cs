using ThermalMaster.Core.Models;

namespace ThermalMaster.Core.Interfaces;

/// <summary>
/// Contrato de persistencia para el registro y actualización de comandos enviados al dispositivo.
/// </summary>
public interface ICommandRepository
{
    Task InsertCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default);
    Task AcknowledgeCommandAsync(Guid commandId, string? responsePayload, DateTime acknowledgedAtUtc, CancellationToken cancellationToken = default);
}
