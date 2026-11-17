using Microsoft.Data.SqlClient;
using ThermalMaster.Core.Interfaces;
using ThermalMaster.Core.Models;

namespace ThermalMaster.Infrastructure.Data;

public class CommandRepository : ICommandRepository
{
    private readonly DatabaseConnectionFactory _factory;

    public CommandRepository(DatabaseConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task InsertCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO dbo.DeviceCommands (CommandId, CommandType, Parameter, IssuedAt, Acknowledged, AcknowledgedAt, ResponsePayload)
VALUES (@CommandId, @CommandType, @Parameter, @IssuedAt, @Acknowledged, @AcknowledgedAt, @ResponsePayload);";

        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@CommandId", command.CommandId);
        cmd.Parameters.AddWithValue("@CommandType", command.CommandType ?? string.Empty);
        cmd.Parameters.AddWithValue("@Parameter", (object?)command.Parameter ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IssuedAt", command.IssuedAt);
        cmd.Parameters.AddWithValue("@Acknowledged", command.AcknowledgedByArduino);
        cmd.Parameters.AddWithValue("@AcknowledgedAt", (object?)command.AcknowledgedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ResponsePayload", DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AcknowledgeCommandAsync(Guid commandId, string? responsePayload, DateTime acknowledgedAtUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE dbo.DeviceCommands
SET Acknowledged = 1,
    AcknowledgedAt = @AcknowledgedAt,
    ResponsePayload = @ResponsePayload
WHERE CommandId = @CommandId;";

        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@CommandId", commandId);
        cmd.Parameters.AddWithValue("@AcknowledgedAt", acknowledgedAtUtc);
        cmd.Parameters.AddWithValue("@ResponsePayload", (object?)responsePayload ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
