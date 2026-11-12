using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Data.Seed;

internal static class DatabaseTests
{
    public static async Task RunAsync(Func<string, Func<Task>, Task> test, string path, bool beforeImport)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FinancialDbContext).Assembly.FullName,
            ContentRootPath = Path.GetFullPath("backend/UrbanSketch.Financial.Api"),
            EnvironmentName = "Development"
        });
        var connection = builder.Configuration.GetConnectionString("DefaultConnection");
        var target = new NpgsqlConnectionStringBuilder(connection);
        Assert(target.Host == "127.0.0.1" && target.Port == 5432 && target.Database == "urbansketch_financial");
        FinancialDbContext Context(bool failAfterSave = false)
        {
            var options = new DbContextOptionsBuilder<FinancialDbContext>().UseNpgsql(connection);
            if (failAfterSave) options.AddInterceptors(new FailAfterSave());
            return new FinancialDbContext(options.Options);
        }
        TepSeedImporter Importer(FinancialDbContext db) =>
            new(db, builder.Environment, NullLogger<TepSeedImporter>.Instance);
        async Task<(int,int,int,int)> Counts()
        {
            await using var db = Context();
            return (await db.Projects.CountAsync(), await db.ConceptVariants.CountAsync(),
                await db.FinancialModels.CountAsync(), await db.CashFlows.CountAsync());
        }
        await using (var check = Context())
        {
            Assert((await check.Database.GetAppliedMigrationsAsync()).Contains("20261111075406_InitialCreate"));
            if (beforeImport) Assert(await Counts() == (0, 0, 0, 0));
        }

        if (beforeImport)
        {
            await test("invalid second concept leaves no partial project", async () =>
            {
                var baseline = await Counts();
                var temporary = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(temporary, (await File.ReadAllTextAsync(path)).Replace("Концепция Б", " "));
                    await using var db = Context();
                    try { await Importer(db).ImportFileAsync(temporary); throw new Exception("Expected rejection"); }
                    catch (SeedImportException) { }
                    Assert(await Counts() == baseline);
                }
                finally { File.Delete(temporary); }
            });
            await test("exception after real INSERT rolls back all records", async () =>
            {
                var baseline = await Counts();
                await using (var db = Context(true))
                {
                    try { await Importer(db).ImportFileAsync(path); throw new Exception("Expected injected failure"); }
                    catch (ForcedFailure) { }
                }
                Assert(await Counts() == baseline);
            });
        }
        else
        {
            await test("variant ID owned by another project aborts without writes", async () =>
            {
                var baseline = await Counts();
                var temporary = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(temporary, (await File.ReadAllTextAsync(path))
                        .Replace("a18d9b8a-3d42-4b92-b7e0-095b3e1a0100", "a18d9b8a-3d42-4b92-b7e0-095b3e1a0199"));
                    await using var db = Context();
                    try { await Importer(db).ImportFileAsync(temporary); throw new Exception("Expected conflict"); }
                    catch (SeedImportException) { }
                    Assert(await Counts() == baseline);
                }
                finally { File.Delete(temporary); }
            });
            await test("two repeated imports preserve records and do not duplicate", async () =>
            {
                var baseline = await Counts();
                Assert(baseline == (1, 2, 0, 0));
                await using var first = Context();
                var original = await first.Projects.AsNoTracking().SingleAsync();
                var result = await Importer(first).ImportFileAsync(path);
                await using var second = Context();
                var repeated = await Importer(second).ImportFileAsync(path);
                Assert(result.ProjectsAdded == 0 && result.VariantsAdded == 0
                    && repeated.ProjectsAdded == 0 && repeated.VariantsAdded == 0);
                Assert(await Counts() == baseline);
                var persisted = await second.Projects.AsNoTracking().SingleAsync();
                Assert(persisted.CreatedAt == original.CreatedAt && persisted.Name == original.Name);
            });
            await test("changed input does not overwrite stored TEP", async () =>
            {
                var temporary = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(temporary, (await File.ReadAllTextAsync(path)).Replace("1000", "777"));
                    await using var db = Context();
                    await Importer(db).ImportFileAsync(temporary);
                    Assert((await db.ConceptVariants.AsNoTracking().SingleAsync(v => v.Name == "Концепция А")).ResidentialArea == 1000m);
                    Assert(await Counts() == (1, 2, 0, 0));
                }
                finally { File.Delete(temporary); }
            });
        }
    }

    private static void Assert(bool condition) { if (!condition) throw new Exception("Database assertion failed"); }
    private sealed class ForcedFailure : Exception;
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default) => throw new ForcedFailure();
    }
}
