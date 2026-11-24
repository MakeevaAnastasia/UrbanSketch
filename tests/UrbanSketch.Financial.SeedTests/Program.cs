using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Data.Seed;

var failures = 0;
var total = 0;
async Task Test(string name, Func<Task> run)
{
    total++;
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.GetType().Name}"); }
}
static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
static async Task Reject(Func<Task> action)
{
    try { await action(); }
    catch (SeedImportException) { return; }
    throw new Exception("Expected SeedImportException");
}
const string valid = """
{"id":"a18d9b8a-3d42-4b92-b7e0-095b3e1a0100","name":"Project","variants":[
{"id":"a18d9b8a-3d42-4b92-b7e0-095b3e1a0101","name":"A","residentialArea":1000,"commercialArea":200,"parkingSpaces":20}]}
""";
static async Task<UrbanSketch.Financial.Api.Models.Project> Parse(string json)
{
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    return await TepSeedReader.ReadAsync(stream);
}
await Test("valid JSON maps entities and FK", async () =>
{
    var project = await Parse(valid);
    var variant = project.Variants.Single();
    Assert(project.Name == "Project" && variant.ResidentialArea == 1000m && variant.CommercialArea == 200m
        && variant.ParkingSpaces == 20 && variant.ProjectId == project.Id && variant.Project == project);
});
await Test("missing optional TEP remain null", async () =>
{
    var v = (await Parse(valid)).Variants.Single();
    Assert(v.ConstructionResidentialArea == null && v.ConstructionCommercialArea == null && v.SocialAreas == null
        && v.ResidentsCount == null && v.BuildingsCount == null && v.MaxFloors == null
        && v.PreschoolPlaces == null && v.SchoolPlaces == null);
});
foreach (var (name, json) in new (string,string)[]
{
    ("missing project name", valid.Replace("\"name\":\"Project\",", "")),
    ("blank project name", valid.Replace("\"Project\"", "\"  \"")),
    ("missing variant name", valid.Replace("\"name\":\"A\",", "")),
    ("null variant name", valid.Replace("\"name\":\"A\"", "\"name\":null")),
    ("missing required area", valid.Replace("\"residentialArea\":1000,", "")),
    ("missing parking", valid.Replace(",\"parkingSpaces\":20", "")),
    ("negative area", valid.Replace("1000", "-1")),
    ("negative parking", valid.Replace("\"parkingSpaces\":20", "\"parkingSpaces\":-1")),
    ("negative optional count", valid.Replace("\"parkingSpaces\":20", "\"parkingSpaces\":20,\"schoolPlaces\":-1")),
    ("negative construction area", valid.Replace("\"parkingSpaces\":20", "\"parkingSpaces\":20,\"constructionResidentialArea\":-1")),
    ("decimal precision overflow", valid.Replace("1000", "10000000000000000")),
    ("decimal scale overflow", valid.Replace("1000", "1.001")),
    ("decimal must not silently underflow", valid.Replace("1000", "0.000000000000000000000000000001")),
    ("int overflow", valid.Replace("\"parkingSpaces\":20", "\"parkingSpaces\":2147483648")),
    ("duplicate identifiers", valid.Replace("0101", "0100")),
    ("empty identifier", valid.Replace("a18d9b8a-3d42-4b92-b7e0-095b3e1a0101", "00000000-0000-0000-0000-000000000000")),
    ("unknown property", valid.Replace("\"name\":\"Project\"", "\"name\":\"Project\",\"unexpected\":1")),
    ("duplicate JSON property", valid.Replace("\"name\":\"Project\"", "\"name\":\"Project\",\"name\":\"Other\"")),
    ("wrong root", "[]"), ("null root", "null"), ("syntax error", "{broken"),
    ("null variant", "{\"id\":\"a18d9b8a-3d42-4b92-b7e0-095b3e1a0100\",\"name\":\"P\",\"variants\":[null]}"),
    ("empty variants", "{\"id\":\"a18d9b8a-3d42-4b92-b7e0-095b3e1a0100\",\"name\":\"P\",\"variants\":[]}")
})
    await Test(name, () => Reject(async () => { await Parse(json); }));
await Test("max numeric(18,2) accepted exactly", async () =>
    Assert((await Parse(valid.Replace("1000", "9999999999999999.99"))).Variants.Single().ResidentialArea == 9999999999999999.99m));
await Test("scientific JSON numbers retain exact value", async () =>
    Assert((await Parse(valid.Replace("1000", "1e3"))).Variants.Single().ResidentialArea == 1000m));
await Test("missing file has safe diagnostic", () => Reject(async () => { await TepSeedReader.ReadFileAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")); }));
await Test("nonlocal database rejected before connection or file access", async () =>
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
    await using var db = new FinancialDbContext(new DbContextOptionsBuilder<FinancialDbContext>()
        .UseNpgsql("Host=example.invalid;Database=other").Options);
    var importer = new TepSeedImporter(db, builder.Environment, NullLogger<TepSeedImporter>.Instance);
    try { await importer.ImportFileAsync("missing.json"); throw new Exception("Expected rejection"); }
    catch (SeedImportException ex) { Assert(ex.Message.Contains("локальную")); }
});
await Test("production rejected before connection or file access", async () =>
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
    await using var db = new FinancialDbContext(new DbContextOptionsBuilder<FinancialDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=urbansketch_financial").Options);
    var importer = new TepSeedImporter(db, builder.Environment, NullLogger<TepSeedImporter>.Instance);
    try { await importer.ImportFileAsync("missing.json"); throw new Exception("Expected rejection"); }
    catch (SeedImportException ex) { Assert(ex.Message.Contains("Development")); }
});

if (args is ["--database-before", var beforePath])
    await DatabaseTests.RunAsync(Test, beforePath, true);
else if (args is ["--database-after", var afterPath])
    await DatabaseTests.RunAsync(Test, afterPath, false);
else if (args.Length != 0)
    throw new ArgumentException("Use --database-before <json> or --database-after <json>, or no arguments for offline tests.");

Console.WriteLine($"Tests: {total}; failures: {failures}");
return failures == 0 ? 0 : 1;
