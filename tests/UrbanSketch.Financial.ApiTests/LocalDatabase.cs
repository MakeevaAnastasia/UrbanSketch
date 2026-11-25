using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Models;

// Ephemeral relational fixture; never opens PostgreSQL or loads secrets.
internal sealed class LocalDatabase(SqliteConnection keeper) : IAsyncDisposable
{
    private static string connectionString = "";
    public static string ReadOnlyConnection() => connectionString;
    public static string TransactionalConnection() => connectionString;
    public static async Task<LocalDatabase> CreateAsync()
    {
        connectionString = $"Data Source=api-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var fixture = new LocalDatabase(keeper);
        try
        {
            await using var db = new SqliteTestContext(new DbContextOptionsBuilder<FinancialDbContext>().UseSqlite(connectionString).Options);
            await db.Database.EnsureCreatedAsync();
            var project = new Project { Id = Guid.Parse("a18d9b8a-3d42-4b92-b7e0-095b3e1a0100"), Name = "Демонстрационный жилой комплекс" };
            db.Projects.Add(project);
            db.ConceptVariants.AddRange(
                new ConceptVariant { Id = Guid.Parse("a18d9b8a-3d42-4b92-b7e0-095b3e1a0101"), Project = project, Name = "Концепция А", ResidentialArea = 1000, CommercialArea = 200, ParkingSpaces = 20 },
                new ConceptVariant { Id = Guid.Parse("a18d9b8a-3d42-4b92-b7e0-095b3e1a0102"), Project = project, Name = "Концепция Б", ResidentialArea = 900, CommercialArea = 300, ParkingSpaces = 25 });
            await db.SaveChangesAsync();
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }
    public static async Task<string[]> SnapshotAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        var rows = new List<string>();
        foreach (var table in new[] { "projects", "concept_variants", "financial_models", "cash_flows" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {table} ORDER BY id";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add(table + System.Text.Json.JsonSerializer.Serialize(Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetValue(i)).ToArray()));
        }
        return rows.ToArray();
    }
    public async ValueTask DisposeAsync() => await keeper.DisposeAsync();
}
