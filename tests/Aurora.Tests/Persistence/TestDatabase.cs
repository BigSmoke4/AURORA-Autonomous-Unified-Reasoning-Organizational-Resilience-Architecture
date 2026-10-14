using Npgsql;

namespace Aurora.Tests.Persistence;

/// <summary>
/// Reads the disposable PostgreSQL connection for integration tests. The database is dropped and
/// recreated by these tests, so the guard refuses any database whose name does not contain "test".
/// </summary>
internal static class TestDatabase
{
    public const string ConnectionVariable = "AURORA_TEST_CONNECTION";

    public static string ReadDisposableDatabaseConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionVariable} to a disposable PostgreSQL database whose name contains 'test'.");
        }

        var database = new NpgsqlConnectionStringBuilder(connectionString).Database ?? string.Empty;
        if (!database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The integration database name must contain 'test' because these tests drop and recreate it.");
        }

        return connectionString;
    }
}
