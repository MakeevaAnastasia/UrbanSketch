using Microsoft.EntityFrameworkCore;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Data.Seed;
using UrbanSketch.Financial.Api.Endpoints;

var seedRequested = args.Contains("--seed-tep", StringComparer.Ordinal);
if (seedRequested && (args.Length != 2 || args[0] != "--seed-tep" || string.IsNullOrWhiteSpace(args[1])))
{
    Console.Error.WriteLine("Использование: --seed-tep <путь к JSON-файлу>.");
    return 2;
}
var builder = WebApplication.CreateBuilder(seedRequested ? [] : args);

builder.Services.AddDbContext<FinancialDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "PostgreSQL connection is not configured. Set ConnectionStrings:DefaultConnection " +
            "using .NET User Secrets or the ConnectionStrings__DefaultConnection environment variable.");
    }

    options.UseNpgsql(connectionString);
});
builder.Services.AddScoped<TepSeedImporter>();

var app = builder.Build();

if (seedRequested)
{
    try
    {
        // Do not resolve the database context or start the HTTP server outside Development.
        if (!app.Environment.IsDevelopment())
            throw new SeedImportException("Импорт демонстрационных ТЭП разрешён только в Development.");
        await using var scope = app.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<TepSeedImporter>().ImportFileAsync(args[1]);
        Console.WriteLine($"Импорт завершён: проектов добавлено {result.ProjectsAdded}, концепций добавлено {result.VariantsAdded}, концепций пропущено {result.VariantsSkipped}.");
        return 0;
    }
    catch (SeedImportException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
    catch (Exception)
    {
        Console.Error.WriteLine("Импорт не завершён. Проверьте файл и локальную конфигурацию PostgreSQL; подробности скрыты для защиты секретов.");
        return 1;
    }
    finally { await app.DisposeAsync(); }
}

app.UseApiErrorHandling();
app.MapProjectsEndpoints();
app.MapConceptVariantsEndpoints();
app.MapFinancialModelsEndpoints();

// Проверка доступности приложения; sprint — временная метка этапа, не версия API.
// Обращение к PostgreSQL не выполняется.
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    component = "UrbanSketch Financial API",
    sprint = 2
}));

app.Run();
return 0;
