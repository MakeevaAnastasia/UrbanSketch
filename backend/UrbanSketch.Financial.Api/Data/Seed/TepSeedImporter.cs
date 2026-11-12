using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace UrbanSketch.Financial.Api.Data.Seed;

public sealed record SeedImportResult(int ProjectsAdded, int VariantsAdded, int VariantsSkipped);

public sealed class TepSeedImporter(
    FinancialDbContext db,
    IHostEnvironment environment,
    ILogger<TepSeedImporter> logger)
{
    public async Task<SeedImportResult> ImportFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
            throw new SeedImportException("Импорт демонстрационных ТЭП разрешён только в Development.");
        NpgsqlConnectionStringBuilder target;
        try { target = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()); }
        catch (ArgumentException) { throw new SeedImportException("Не удалось проверить локальное подключение PostgreSQL."); }
        if (target.Host is not ("127.0.0.1" or "localhost" or "::1") || target.Port != 5432
            || target.Database != "urbansketch_financial")
            throw new SeedImportException("Импорт разрешён только в локальную urbansketch_financial на порту 5432.");
        if (db.ChangeTracker.HasChanges())
            throw new SeedImportException("Для импорта требуется отдельный контекст без несохранённых изменений.");

        // Validate the entire file before opening a connection or scheduling any INSERT.
        var incoming = await TepSeedReader.ReadFileAsync(path, cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // Serialize this import command across processes; the lock ends on commit or rollback.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7524332010100)", cancellationToken);
            var database = await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"")
                .SingleAsync(cancellationToken);
            if (database != "urbansketch_financial")
                throw new SeedImportException("Фактическая база не соответствует локальной базе проекта.");
            var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
            if (!applied.Contains("20261111075406_InitialCreate")
                || (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                throw new SeedImportException("Необходимо применить подготовленные миграции до импорта ТЭП.");

            logger.LogInformation("Импорт в локальную {Database}, порт {Port}. До импорта: проекты {Projects}, концепции {Variants}, модели {Models}, потоки {CashFlows}.",
                database, target.Port, await db.Projects.CountAsync(cancellationToken),
                await db.ConceptVariants.CountAsync(cancellationToken),
                await db.FinancialModels.CountAsync(cancellationToken), await db.CashFlows.CountAsync(cancellationToken));

            var variants = incoming.Variants.ToArray();
            var ids = variants.Select(v => v.Id).ToArray();
            var existingVariants = await db.ConceptVariants.AsNoTracking().Where(v => ids.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken);
            if (existingVariants.Values.Any(v => v.ProjectId != incoming.Id))
                throw new SeedImportException("Идентификатор концепции уже принадлежит другому проекту; импорт отменён.");

            var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == incoming.Id, cancellationToken);
            var projectsAdded = project == null ? 1 : 0;
            if (project == null)
            {
                project = incoming;
                project.Variants.Clear();
                db.Projects.Add(project);
            }
            var variantsAdded = 0;
            foreach (var variant in variants)
            {
                if (existingVariants.ContainsKey(variant.Id)) continue;
                variant.Project = project;
                db.ConceptVariants.Add(variant);
                variantsAdded++;
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SeedImportResult(projectsAdded, variantsAdded, variants.Length - variantsAdded);
        }
        catch (DbUpdateException)
        {
            throw new SeedImportException("Ошибка сохранения ТЭП. Импорт выполняется одной транзакцией; проверьте состояние базы перед повтором.");
        }
        catch (DbException)
        {
            throw new SeedImportException("Не удалось выполнить импорт в локальную PostgreSQL. Проверьте доступность базы и миграции.");
        }
    }
}
