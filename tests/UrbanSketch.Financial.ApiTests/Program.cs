using System.Net;
using System.Text.Json;

const string projectId = "a18d9b8a-3d42-4b92-b7e0-095b3e1a0100";
const string conceptA = "a18d9b8a-3d42-4b92-b7e0-095b3e1a0101";
const string conceptB = "a18d9b8a-3d42-4b92-b7e0-095b3e1a0102";
const string absentId = "a18d9b8a-3d42-4b92-b7e0-095b3e1a0199";
var failures = 0;
var total = 0;
static void Assert(bool condition, string message = "Response assertion failed")
{
    if (!condition) throw new TestFailure(message);
}
async Task Test(string name, Func<Task> run)
{
    total++;
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (TestFailure ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.GetType().Name}; details suppressed"); }
}
static async Task<JsonElement> GetJson(HttpClient client, string path)
{
    using var response = await client.GetAsync(path);
    Assert(response.StatusCode == HttpStatusCode.OK, $"Expected HTTP 200, got {(int)response.StatusCode}");
    Assert(response.Content.Headers.ContentType?.MediaType == "application/json");
    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    return json.RootElement.Clone();
}
static async Task NotFound(HttpClient client, string path)
{
    using var response = await client.GetAsync(path);
    Assert(response.StatusCode == HttpStatusCode.NotFound, $"Expected HTTP 404, got {(int)response.StatusCode}");
}
try
{
    var connection = LocalDatabase.ReadOnlyConnection();
    var before = await LocalDatabase.SnapshotAsync(connection);
    await using var normal = await ApiTestHost.StartAsync(connection);
    await Test("projects list uses DTO camelCase shape", async () =>
    {
        var json = await GetJson(normal.Client, "/api/projects");
        Assert(json.GetArrayLength() == 1);
        var item = json[0];
        Assert(item.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "createdAt", "id", "name" }));
        Assert(item.GetProperty("id").GetGuid() == Guid.Parse(projectId));
        Assert(item.GetProperty("name").GetString() == "Демонстрационный жилой комплекс");
        Assert(item.GetProperty("createdAt").GetDateTimeOffset().Offset == TimeSpan.Zero);
    });
    await Test("specific project returns its metadata", async () =>
    {
        var json = await GetJson(normal.Client, $"/api/projects/{projectId}");
        Assert(json.GetProperty("id").GetGuid() == Guid.Parse(projectId));
        Assert(json.EnumerateObject().Count() == 3 && json.GetProperty("createdAt").GetDateTimeOffset() != default);
    });
    await Test("project variants sorted with exact sale areas and parking", async () =>
    {
        var json = await GetJson(normal.Client, $"/api/projects/{projectId}/variants");
        Assert(json.GetArrayLength() == 2);
        Assert(json[0].GetProperty("name").GetString() == "Концепция А" && json[1].GetProperty("name").GetString() == "Концепция Б");
        foreach (var (index, id, residential, commercial, parking) in new[]
        { (0, conceptA, 1000m, 200m, 20), (1, conceptB, 900m, 300m, 25) })
        {
            var item = json[index];
            Assert(item.EnumerateObject().Count() == 6 && item.GetProperty("id").GetGuid() == Guid.Parse(id));
            Assert(item.GetProperty("projectId").GetGuid() == Guid.Parse(projectId));
            Assert(item.GetProperty("residentialArea").GetDecimal() == residential && item.GetProperty("commercialArea").GetDecimal() == commercial);
            Assert(item.GetProperty("parkingSpaces").GetInt32() == parking);
        }
    });
    foreach (var (id, residential, commercial, parking) in new[] { (conceptA, 1000m, 200m, 20), (conceptB, 900m, 300m, 25) })
        await Test($"concept detail {id}", async () =>
        {
            var json = await GetJson(normal.Client, $"/api/concepts/{id}");
            Assert(json.EnumerateObject().Count() == 14 && json.GetProperty("id").GetGuid() == Guid.Parse(id));
            Assert(json.GetProperty("projectId").GetGuid() == Guid.Parse(projectId));
            Assert(json.GetProperty("residentialArea").GetDecimal() == residential && json.GetProperty("commercialArea").GetDecimal() == commercial);
            Assert(json.GetProperty("parkingSpaces").GetInt32() == parking);
        });
    await Test("unknown construction areas and optional TEP are JSON null", async () =>
    {
        foreach (var id in new[] { conceptA, conceptB })
        {
            var json = await GetJson(normal.Client, $"/api/concepts/{id}");
            foreach (var field in new[] { "constructionResidentialArea", "constructionCommercialArea", "residentsCount", "buildingsCount", "maxFloors", "socialAreas", "preschoolPlaces", "schoolPlaces" })
                Assert(json.GetProperty(field).ValueKind == JsonValueKind.Null, $"Expected null for {field}");
        }
    });
    await Test("missing project is 404", () => NotFound(normal.Client, $"/api/projects/{absentId}"));
    await Test("variants of missing project are 404", () => NotFound(normal.Client, $"/api/projects/{absentId}/variants"));
    await Test("missing concept is 404", () => NotFound(normal.Client, $"/api/concepts/{absentId}"));
    foreach (var path in new[] { "/api/projects/not-a-guid", "/api/projects/not-a-guid/variants", "/api/concepts/not-a-guid" })
        await Test($"malformed Guid: {path}", () => NotFound(normal.Client, path));
    await Test("empty Guid is a missing project, not 500", () => NotFound(normal.Client, $"/api/projects/{Guid.Empty}"));
    await using var emptyProjects = await ApiTestHost.StartAsync(connection, TestMode.EmptyProjects);
    await Test("empty projects list is HTTP 200 and []", async () =>
        Assert((await GetJson(emptyProjects.Client, "/api/projects")).GetArrayLength() == 0));
    await using var emptyVariants = await ApiTestHost.StartAsync(connection, TestMode.EmptyVariants);
    await Test("existing project without variants is HTTP 200 and []", async () =>
        Assert((await GetJson(emptyVariants.Client, $"/api/projects/{projectId}/variants")).GetArrayLength() == 0));
    await Test("empty variants do not turn missing project into 200", () => NotFound(emptyVariants.Client, $"/api/projects/{absentId}/variants"));
    await using var broken = await ApiTestHost.StartAsync(connection, TestMode.DatabaseFailure);
    await Test("database failure is safe HTTP 500, never []", async () =>
    {
        using var response = await broken.Client.GetAsync("/api/projects");
        Assert(response.StatusCode == HttpStatusCode.InternalServerError);
        Assert(response.Content.Headers.ContentType?.MediaType == "application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert(json.RootElement.GetProperty("status").GetInt32() == 500);
        Assert(!body.Contains("PRIVATE_DATABASE_DIAGNOSTIC_SENTINEL") && !body.Contains("StackTrace") && !body.Contains("InvalidOperationException"));
        Assert(!json.RootElement.TryGetProperty("detail", out var detail) || detail.ValueKind == JsonValueKind.Null);
    });
    await FinancialModelTests.RunAsync(Test);
    await Test("GET and rolled-back CRUD leave PostgreSQL data and history unchanged", async () =>
        Assert(Enumerable.SequenceEqual(before, await LocalDatabase.SnapshotAsync(connection))));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"API test setup failed: {ex.GetType().Name}. Check local PostgreSQL and Development User Secrets; sensitive details suppressed.");
    return 2;
}
Console.WriteLine($"API tests: {total}; failures: {failures}");
return failures == 0 ? 0 : 1;

internal sealed class TestFailure(string message) : Exception(message);
