using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Npgsql;
using UrbanSketch.Financial.Api.Data;

internal static class LocalDatabase
{
    public static string ReadOnlyConnection() => Connection(true);

    public static string TransactionalConnection() => Connection(false);

    private static string Connection(bool readOnly)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FinancialDbContext).Assembly.FullName,
            ContentRootPath = Path.GetFullPath("backend/UrbanSketch.Financial.Api"),
            EnvironmentName = "Development"
        });
        var connection = new NpgsqlConnectionStringBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
        if (connection.Host != "127.0.0.1" || connection.Port != 5432 || connection.Database != "urbansketch_financial")
            throw new InvalidOperationException("API tests require the confirmed local project database.");
        if (readOnly) connection.Options = (connection.Options + " -c default_transaction_read_only=on").Trim();
        connection.ApplicationName = readOnly ? "UrbanSketch.ApiReadOnlyTests" : "UrbanSketch.ApiTransactionTests";
        return connection.ConnectionString;
    }

    public static async Task<string[]> SnapshotAsync(string readOnlyConnection)
    {
        await using var connection = new NpgsqlConnection(readOnlyConnection);
        await connection.OpenAsync();
        await using (var guard = new NpgsqlCommand("SHOW default_transaction_read_only", connection))
            if ((string?)await guard.ExecuteScalarAsync() != "on") throw new InvalidOperationException("Read-only guard is not active.");
        var snapshot = new List<string>();
        foreach (var table in new[] { "projects", "concept_variants", "financial_models", "cash_flows", "\"__EFMigrationsHistory\"" })
        {
            var key = table.StartsWith('"') ? "\"MigrationId\"" : "id";
            await using var command = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY {key})::text, '[]') FROM public.{table} t", connection);
            snapshot.Add((string)(await command.ExecuteScalarAsync())!);
        }
        return snapshot.ToArray();
    }
}
