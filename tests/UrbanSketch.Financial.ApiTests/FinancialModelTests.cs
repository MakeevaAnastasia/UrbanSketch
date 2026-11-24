using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Models;

internal static class FinancialModelTests
{
    private const string ConceptA = "a18d9b8a-3d42-4b92-b7e0-095b3e1a0101";
    private const string ConceptB = "a18d9b8a-3d42-4b92-b7e0-095b3e1a0102";
    private const string Absent = "a18d9b8a-3d42-4b92-b7e0-095b3e1a0199";
    private const string Parameters = """
        "residentialConstructionCost":100000,"commercialConstructionCost":80000,"parkingCost":500000,
        "residentialSalePrice":200000,"commercialSalePrice":150000,"parkingSalePrice":1000000,
        "landCost":50000000,"additionalCosts":20000000,"projectDuration":3
        """;
    private const string UpdatedParameters = """
        "residentialConstructionCost":123456.78,"commercialConstructionCost":91000.12,"parkingCost":610000.34,
        "residentialSalePrice":210000.56,"commercialSalePrice":160000.78,"parkingSalePrice":1100000.90,
        "landCost":55000000.12,"additionalCosts":21000000.34,"projectDuration":4
        """;
    private static string Create(string concept = ConceptA) => "{\"conceptVariantId\":\"" + concept + "\"," + Parameters + "}";
    private static void Assert(bool ok, string message = "Financial API assertion failed") { if (!ok) throw new TestFailure(message); }
    private static async Task<(HttpStatusCode Status, JsonElement Json, Uri? Location)> Send(
        HttpClient client, HttpMethod method, string path, string? json = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        JsonElement element = default;
        if (body.Length != 0)
        {
            using var document = JsonDocument.Parse(body);
            element = document.RootElement.Clone();
        }
        return (response.StatusCode, element, response.Headers.Location);
    }
    private static void AssertParameters(JsonElement response, string expected)
    {
        using var document = JsonDocument.Parse("{" + expected + "}");
        foreach (var field in document.RootElement.EnumerateObject())
            Assert(response.GetProperty(field.Name).GetDecimal() == field.Value.GetDecimal(), $"Parameter mismatch: {field.Name}");
    }
    private static void UnknownResults(JsonElement json)
    {
        foreach (var field in new[] { "totalCosts", "totalRevenue", "profit", "roi", "salesProfitability" })
            Assert(json.GetProperty(field).ValueKind == JsonValueKind.Null);
        Assert(!json.TryGetProperty("conceptVariant", out _) && !json.TryGetProperty("cashFlows", out _));
    }

    public static async Task RunAsync(Func<string, Func<Task>, Task> test)
    {
        await using var connection = new NpgsqlConnection(LocalDatabase.TransactionalConnection());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        FinancialDbContext Context()
        {
            var context = new FinancialDbContext(new DbContextOptionsBuilder<FinancialDbContext>()
                .UseNpgsql(connection, contextOwnsConnection: false).Options);
            context.Database.UseTransaction(transaction);
            return context;
        }
        await using var host = await ApiTestHost.StartAsync(LocalDatabase.ReadOnlyConnection(), contextFactory: Context);
        var client = host.Client;
        Guid first = default, second = default, other = default;
        try
        {
            await test("POST creates financial model with Location and null results", async () =>
            {
                var result = await Send(client, HttpMethod.Post, "/api/financial-models", Create());
                Assert(result.Status == HttpStatusCode.Created, $"Expected 201, got {(int)result.Status}");
                first = result.Json.GetProperty("id").GetGuid();
                Assert(first != Guid.Empty && result.Location != null && result.Location.ToString().EndsWith(first.ToString()));
                Assert((await client.GetAsync(result.Location)).StatusCode == HttpStatusCode.OK);
                Assert(result.Json.GetProperty("conceptVariantId").GetGuid() == Guid.Parse(ConceptA));
                Assert(result.Json.GetProperty("residentialConstructionCost").GetDecimal() == 100000m);
                UnknownResults(result.Json);
            });
            await test("GET reads created financial model from PostgreSQL", async () =>
            {
                var result = await Send(client, HttpMethod.Get, $"/api/financial-models/{first}");
                Assert(result.Status == HttpStatusCode.OK && result.Json.GetProperty("id").GetGuid() == first);
                UnknownResults(result.Json);
                await using var db = Context();
                Assert(await db.FinancialModels.AsNoTracking().AnyAsync(m => m.Id == first && m.ProjectDuration == 3));
            });
            await test("multiple models can belong to one concept", async () =>
            {
                var result = await Send(client, HttpMethod.Post, "/api/financial-models", Create());
                Assert(result.Status == HttpStatusCode.Created);
                second = result.Json.GetProperty("id").GetGuid();
                Assert(second != first);
                var list = await Send(client, HttpMethod.Get, $"/api/concepts/{ConceptA}/financial-models");
                Assert(list.Status == HttpStatusCode.OK && list.Json.GetArrayLength() == 2);
                var ids = list.Json.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToArray();
                Assert(ids.Contains(first) && ids.Contains(second));
                var again = await Send(client, HttpMethod.Get, $"/api/concepts/{ConceptA}/financial-models");
                Assert(list.Json.GetRawText() == again.Json.GetRawText());
            });
            await test("other concept initially returns HTTP 200 and []", async () =>
            {
                var list = await Send(client, HttpMethod.Get, $"/api/concepts/{ConceptB}/financial-models");
                Assert(list.Status == HttpStatusCode.OK && list.Json.GetArrayLength() == 0);
            });
            await test("list excludes models of another concept", async () =>
            {
                var created = await Send(client, HttpMethod.Post, "/api/financial-models", Create(ConceptB));
                Assert(created.Status == HttpStatusCode.Created);
                other = created.Json.GetProperty("id").GetGuid();
                var list = await Send(client, HttpMethod.Get, $"/api/concepts/{ConceptA}/financial-models");
                Assert(list.Json.GetArrayLength() == 2 && list.Json.EnumerateArray().All(e => e.GetProperty("id").GetGuid() != other));
            });
            await test("PUT updates parameters while keeping identity, owner and null results", async () =>
            {
                var result = await Send(client, HttpMethod.Put, $"/api/financial-models/{first}", "{" + UpdatedParameters + "}");
                Assert(result.Status == HttpStatusCode.OK && result.Json.GetProperty("id").GetGuid() == first);
                Assert(result.Json.GetProperty("conceptVariantId").GetGuid() == Guid.Parse(ConceptA));
                Assert(result.Json.GetProperty("residentialConstructionCost").GetDecimal() == 123456.78m);
                AssertParameters(result.Json, UpdatedParameters);
                UnknownResults(result.Json);
            });
            await test("PUT persists exact new parameters and leaves neighboring models unchanged", async () =>
            {
                var changed = await Send(client, HttpMethod.Get, $"/api/financial-models/{first}");
                AssertParameters(changed.Json, UpdatedParameters);
                foreach (var id in new[] { second, other })
                {
                    var untouched = await Send(client, HttpMethod.Get, $"/api/financial-models/{id}");
                    AssertParameters(untouched.Json, Parameters);
                }
            });
            var invalid = new Dictionary<string, string>
            {
                ["negative construction cost"] = Create().Replace("\"residentialConstructionCost\":100000", "\"residentialConstructionCost\":-1"),
                ["negative sale price"] = Create().Replace("\"parkingSalePrice\":1000000", "\"parkingSalePrice\":-1"),
                ["zero duration"] = Create().Replace("\"projectDuration\":3", "\"projectDuration\":0"),
                ["negative duration"] = Create().Replace("\"projectDuration\":3", "\"projectDuration\":-1"),
                ["missing fields"] = "{}",
                ["null field"] = Create().Replace("\"landCost\":50000000", "\"landCost\":null"),
                ["numeric string"] = Create().Replace("\"landCost\":50000000", "\"landCost\":\"50000000\""),
                ["boolean"] = Create().Replace("\"landCost\":50000000", "\"landCost\":true"),
                ["fractional duration"] = Create().Replace("\"projectDuration\":3", "\"projectDuration\":1.5"),
                ["int overflow"] = Create().Replace("\"projectDuration\":3", "\"projectDuration\":2147483648"),
                ["decimal overflow"] = Create().Replace("\"landCost\":50000000", "\"landCost\":1000000000000000000"),
                ["decimal scale"] = Create().Replace("\"landCost\":50000000", "\"landCost\":1.001"),
                ["decimal underflow"] = Create().Replace("\"landCost\":50000000", "\"landCost\":0.000000000000000000000000000001"),
                ["NaN token"] = Create().Replace("\"landCost\":50000000", "\"landCost\":NaN"),
                ["NaN string"] = Create().Replace("\"landCost\":50000000", "\"landCost\":\"NaN\""),
                ["malformed Guid"] = Create("not-a-guid"),
                ["empty Guid"] = Create(Guid.Empty.ToString()),
                ["client ID"] = Create().Replace("{", "{\"id\":\"" + Absent + "\","),
                ["client result"] = Create().Replace("{", "{\"totalCosts\":123,"),
                ["duplicate field"] = Create().Replace("{", "{\"landCost\":1,"),
                ["null root"] = "null", ["array root"] = "[]", ["syntax error"] = "{bad"
            };
            foreach (var field in new[] { "commercialConstructionCost", "parkingCost", "residentialSalePrice", "commercialSalePrice", "landCost", "additionalCosts" })
            {
                using var baseRequest = JsonDocument.Parse(Create());
                var original = baseRequest.RootElement.GetProperty(field).GetRawText();
                invalid["negative " + field] = Create().Replace($"\"{field}\":{original}", $"\"{field}\":-1");
            }
            int beforeInvalid;
            await using (var countDb = Context()) beforeInvalid = await countDb.FinancialModels.CountAsync();
            foreach (var (name, body) in invalid)
                await test("POST rejects " + name + " with safe validation problem", async () =>
                {
                    var result = await Send(client, HttpMethod.Post, "/api/financial-models", body);
                    Assert(result.Status == HttpStatusCode.BadRequest, $"Expected 400, got {(int)result.Status}");
                    Assert(result.Json.GetProperty("status").GetInt32() == 400 && result.Json.GetProperty("errors").EnumerateObject().Any());
                    var text = result.Json.GetRawText();
                    Assert(!text.Contains("StackTrace") && !text.Contains("ConnectionStrings") && !text.Contains("JsonException"));
                });
            await test("all rejected POST bodies leave model count unchanged", async () =>
            {
                await using var db = Context();
                Assert(await db.FinancialModels.CountAsync() == beforeInvalid);
            });
            await test("POST accepts numeric(20,2) boundary without rounding", async () =>
            {
                var result = await Send(client, HttpMethod.Post, "/api/financial-models", Create().Replace("\"landCost\":50000000", "\"landCost\":999999999999999999.99"));
                Assert(result.Status == HttpStatusCode.Created && result.Json.GetProperty("landCost").GetDecimal() == 999999999999999999.99m);
                var saved = await Send(client, HttpMethod.Get, $"/api/financial-models/{result.Json.GetProperty("id").GetGuid()}");
                Assert(saved.Json.GetProperty("landCost").GetDecimal() == 999999999999999999.99m);
            });
            await test("zero money and positive int maximum duration are accepted", async () =>
            {
                const string zeroFields = """
                    "residentialConstructionCost":0,"commercialConstructionCost":0,"parkingCost":0,
                    "residentialSalePrice":0,"commercialSalePrice":0,"parkingSalePrice":0,
                    "landCost":0,"additionalCosts":0,"projectDuration":2147483647
                    """;
                var result = await Send(client, HttpMethod.Post, "/api/financial-models", "{\"conceptVariantId\":\"" + ConceptA + "\"," + zeroFields + "}");
                Assert(result.Status == HttpStatusCode.Created);
                AssertParameters(result.Json, zeroFields);
            });
            await test("exact scientific money and insignificant trailing zeroes are accepted", async () =>
            {
                var result = await Send(client, HttpMethod.Post, "/api/financial-models", Create().Replace("\"landCost\":50000000", "\"landCost\":1e3").Replace("\"additionalCosts\":20000000", "\"additionalCosts\":1.2300"));
                Assert(result.Status == HttpStatusCode.Created && result.Json.GetProperty("landCost").GetDecimal() == 1000m && result.Json.GetProperty("additionalCosts").GetDecimal() == 1.23m);
            });
            await test("PUT rejects attempted owner or result reassignment", async () =>
            {
                foreach (var extra in new[] { "\"conceptVariantId\":\"" + ConceptB + "\",", "\"id\":\"" + Absent + "\",", "\"profit\":7," })
                    Assert((await Send(client, HttpMethod.Put, $"/api/financial-models/{first}", "{" + extra + Parameters + "}")).Status == HttpStatusCode.BadRequest);
            });
            await test("invalid PUT does not partially change stored parameters", async () =>
            {
                var result = await Send(client, HttpMethod.Put, $"/api/financial-models/{first}", "{" + Parameters.Replace("\"additionalCosts\":20000000", "\"additionalCosts\":-1") + "}");
                Assert(result.Status == HttpStatusCode.BadRequest);
                var actual = await Send(client, HttpMethod.Get, $"/api/financial-models/{first}");
                Assert(actual.Json.GetProperty("residentialConstructionCost").GetDecimal() == 123456.78m);
            });
            foreach (var (method, path, body) in new[]
            {
                (HttpMethod.Get, $"/api/financial-models/{Absent}", (string?)null),
                (HttpMethod.Put, $"/api/financial-models/{Absent}", "{" + Parameters + "}"),
                (HttpMethod.Delete, $"/api/financial-models/{Absent}", (string?)null),
                (HttpMethod.Get, $"/api/concepts/{Absent}/financial-models", (string?)null),
                (HttpMethod.Post, "/api/financial-models", Create(Absent)),
                (HttpMethod.Get, "/api/financial-models/not-a-guid", (string?)null)
            })
                await test($"{method} {path} unknown ID is 404", async () => Assert((await Send(client, method, path, body)).Status == HttpStatusCode.NotFound));
            await test("DELETE removes only chosen model and cascades its test CashFlow", async () =>
            {
                var flowId = Guid.NewGuid();
                await using (var db = Context())
                {
                    db.CashFlows.Add(new CashFlow { Id = flowId, FinancialModelId = first, PeriodNumber = 1, CostShare = 100m, RevenueShare = 100m });
                    await db.SaveChangesAsync();
                }
                Assert((await Send(client, HttpMethod.Delete, $"/api/financial-models/{first}")).Status == HttpStatusCode.NoContent);
                Assert((await Send(client, HttpMethod.Get, $"/api/financial-models/{first}")).Status == HttpStatusCode.NotFound);
                await using var check = Context();
                Assert(!await check.CashFlows.AnyAsync(f => f.Id == flowId));
                Assert(await check.FinancialModels.AnyAsync(m => m.Id == second) && await check.FinancialModels.AnyAsync(m => m.Id == other));
                Assert(await check.Projects.CountAsync() == 1 && await check.ConceptVariants.CountAsync() == 2);
            });
            await test("mass DELETE is unavailable and leaves other models intact", async () =>
            {
                var result = await Send(client, HttpMethod.Delete, "/api/financial-models");
                Assert(result.Status == HttpStatusCode.MethodNotAllowed);
                await using var db = Context();
                Assert(await db.FinancialModels.AnyAsync(m => m.Id == second) && await db.FinancialModels.AnyAsync(m => m.Id == other));
            });
            await test("financial CRUD internal failures return safe ProblemDetails 500", async () =>
            {
                await using var failedHost = await ApiTestHost.StartAsync(LocalDatabase.ReadOnlyConnection(), TestMode.DatabaseFailure);
                var result = await Send(failedHost.Client, HttpMethod.Post, "/api/financial-models", Create());
                Assert(result.Status == HttpStatusCode.InternalServerError && !result.Json.GetRawText().Contains("PRIVATE_DATABASE_DIAGNOSTIC_SENTINEL"));
                Assert(!result.Json.TryGetProperty("detail", out var detail) || detail.ValueKind == JsonValueKind.Null);
            });
        }
        finally { await transaction.RollbackAsync(); }
    }
}
