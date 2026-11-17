using Microsoft.Data.SqlClient;

namespace ThermalMaster.Infrastructure.Data;

/// <summary>
/// Proveedor de conexiones a la base de datos SQL Server.
/// </summary>
public class DatabaseConnectionFactory
{
    public string ConnectionString { get; }

    public DatabaseConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(connectionString));

        var cs = connectionString.Trim();
        if (!cs.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase) &&
            !cs.Contains("Connection Timeout", StringComparison.OrdinalIgnoreCase))
        {
            cs = cs.TrimEnd(';') + ";Connect Timeout=5";
        }
        ConnectionString = cs;
    }

    public SqlConnection CreateOpenConnection()
    {
        var connection = new SqlConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    public async Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
