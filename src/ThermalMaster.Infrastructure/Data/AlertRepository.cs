using Microsoft.Data.SqlClient;
using ThermalMaster.Core.Enums;
using ThermalMaster.Core.Interfaces;
using ThermalMaster.Core.Models;

namespace ThermalMaster.Infrastructure.Data;

public class AlertRepository : IAlertRepository
{
    private readonly DatabaseConnectionFactory _factory;

    public AlertRepository(DatabaseConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<long> LogAlertAsync(AlertLog alert, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO dbo.AlertLogs (Timestamp, Severity, Message, TriggerTemperature, TriggerHumidity, IsResolved, ResolvedAt)
OUTPUT INSERTED.AlertId
VALUES (@Timestamp, @Severity, @Message, @TriggerTemperature, @TriggerHumidity, @IsResolved, @ResolvedAt);";

        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Timestamp", alert.Timestamp);
        cmd.Parameters.AddWithValue("@Severity", ToDbSeverity(alert.Severity));
        cmd.Parameters.AddWithValue("@Message", alert.Message ?? string.Empty);
        cmd.Parameters.AddWithValue("@TriggerTemperature", alert.TriggerTemperature);
        cmd.Parameters.AddWithValue("@TriggerHumidity", (object?)alert.TriggerHumidity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsResolved", alert.IsResolved);
        cmd.Parameters.AddWithValue("@ResolvedAt", (object?)alert.ResolvedAt ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is long id ? id : Convert.ToInt64(result);
    }

    public async Task<IReadOnlyList<AlertLog>> GetRecentAlertsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP (@Limit) AlertId, Timestamp, Severity, Message, TriggerTemperature, TriggerHumidity, IsResolved, ResolvedAt
FROM dbo.AlertLogs
ORDER BY Timestamp DESC;";

        var list = new List<AlertLog>();
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new AlertLog
            {
                Id = reader.GetInt64(0),
                Timestamp = reader.GetDateTime(1),
                Severity = ParseSeverity(reader.GetString(2)),
                Message = reader.GetString(3),
                TriggerTemperature = (double)reader.GetDecimal(4),
                TriggerHumidity = reader.IsDBNull(5) ? null : (double)reader.GetDecimal(5),
                IsResolved = reader.GetBoolean(6),
                ResolvedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
            });
        }
        return list;
    }

    public async Task ResolveAlertAsync(long alertId, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE dbo.AlertLogs SET IsResolved = 1, ResolvedAt = SYSUTCDATETIME() WHERE AlertId = @AlertId;";
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@AlertId", alertId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ToDbSeverity(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "CRITICAL",
        AlertSeverity.Warning => "WARNING",
        _ => "NORMAL"
    };

    private static AlertSeverity ParseSeverity(string value)
    {
        return value?.Trim().ToUpperInvariant() switch
        {
            "CRITICAL" => AlertSeverity.Critical,
            "WARNING" => AlertSeverity.Warning,
            _ => AlertSeverity.Normal
        };
    }
}
