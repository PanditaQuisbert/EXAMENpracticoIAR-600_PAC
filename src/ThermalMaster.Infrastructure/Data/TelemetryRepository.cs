using Microsoft.Data.SqlClient;
using ThermalMaster.Core.Enums;
using ThermalMaster.Core.Interfaces;
using ThermalMaster.Core.Models;

namespace ThermalMaster.Infrastructure.Data;

public class TelemetryRepository : ITelemetryRepository
{
    private readonly DatabaseConnectionFactory _factory;

    public TelemetryRepository(DatabaseConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<long> InsertTelemetryAsync(TelemetryRecord record, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO dbo.SensorReadings
    (Timestamp, TemperatureC, HumidityPct, FanStatus, LampStatus, OperatingMode, Severity, MinThreshold, MaxThreshold, ProteusAck)
OUTPUT INSERTED.ReadingId
VALUES
    (@Timestamp, @TemperatureC, @HumidityPct, @FanStatus, @LampStatus, @OperatingMode, @Severity, @MinThreshold, @MaxThreshold, @ProteusAck);";

        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Timestamp", record.Timestamp);
        cmd.Parameters.AddWithValue("@TemperatureC", record.TemperatureC);
        cmd.Parameters.AddWithValue("@HumidityPct", record.HumidityPct);
        cmd.Parameters.AddWithValue("@FanStatus", record.FanStatus == FanState.On);
        cmd.Parameters.AddWithValue("@LampStatus", record.LampStatus == LampState.On);
        cmd.Parameters.AddWithValue("@OperatingMode", ToDbMode(record.Mode));
        cmd.Parameters.AddWithValue("@Severity", ToDbSeverity(record.Severity));
        cmd.Parameters.AddWithValue("@MinThreshold", record.MinThreshold);
        cmd.Parameters.AddWithValue("@MaxThreshold", record.MaxThreshold);
        cmd.Parameters.AddWithValue("@ProteusAck", record.ProteusAcknowledged);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is long id ? id : Convert.ToInt64(result);
    }

    public async Task<IReadOnlyList<TelemetryRecord>> GetHistoricalReadingsAsync(DateTime from, DateTime to, int limit = 500, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP (@Limit)
    ReadingId, Timestamp, TemperatureC, HumidityPct, FanStatus, LampStatus, OperatingMode, Severity, MinThreshold, MaxThreshold, ProteusAck
FROM dbo.SensorReadings
WHERE Timestamp >= @From AND Timestamp <= @To
ORDER BY Timestamp DESC;";

        var list = new List<TelemetryRecord>();
        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Limit", limit);
        cmd.Parameters.AddWithValue("@From", from);
        cmd.Parameters.AddWithValue("@To", to);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(Map(reader));
        }
        return list;
    }

    public async Task<TelemetryRecord?> GetLatestReadingAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1
    ReadingId, Timestamp, TemperatureC, HumidityPct, FanStatus, LampStatus, OperatingMode, Severity, MinThreshold, MaxThreshold, ProteusAck
FROM dbo.SensorReadings
ORDER BY Timestamp DESC;";

        await using var connection = await _factory.CreateOpenConnectionAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            return Map(reader);
        return null;
    }

    private static TelemetryRecord Map(SqlDataReader reader)
    {
        return new TelemetryRecord
        {
            Id = reader.GetInt64(0),
            Timestamp = reader.GetDateTime(1),
            TemperatureC = (double)reader.GetDecimal(2),
            HumidityPct = (double)reader.GetDecimal(3),
            FanStatus = reader.GetBoolean(4) ? FanState.On : FanState.Off,
            LampStatus = reader.GetBoolean(5) ? LampState.On : LampState.Off,
            Mode = ParseMode(reader.GetString(6)),
            Severity = ParseSeverity(reader.GetString(7)),
            MinThreshold = (double)reader.GetDecimal(8),
            MaxThreshold = (double)reader.GetDecimal(9),
            ProteusAcknowledged = reader.GetBoolean(10)
        };
    }

    private static string ToDbMode(OperatingMode mode) => mode == OperatingMode.Manual ? "MANUAL" : "AUTOMATIC";

    private static string ToDbSeverity(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "CRITICAL",
        AlertSeverity.Warning => "WARNING",
        _ => "NORMAL"
    };

    private static OperatingMode ParseMode(string value)
    {
        return value?.Trim().ToUpperInvariant() switch
        {
            "MANUAL" or "MANUAL_MODE" => OperatingMode.Manual,
            _ => OperatingMode.Automatic
        };
    }

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
