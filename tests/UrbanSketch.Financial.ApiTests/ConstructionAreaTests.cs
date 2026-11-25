using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UrbanSketch.Financial.Api.Data;

internal static class ConstructionAreaTests
{
    public static async Task RunAsync(Func<string, Func<Task>, Task> test, HttpClient client, Guid first, Guid second, Func<FinancialDbContext> context)
    {
        const string fields = "\"residentialConstructionCost\":123456.78,\"commercialConstructionCost\":91000.12,\"parkingCost\":610000.34,\"residentialSalePrice\":210000.56,\"commercialSalePrice\":160000.78,\"parkingSalePrice\":1100000.90,\"landCost\":55000000.12,\"additionalCosts\":21000000.34,\"projectDuration\":4";
        const string r = "constructionResidentialAreaOverride", c = "constructionCommercialAreaOverride";
        static void Check(bool value) { if (!value) throw new TestFailure("Construction area assertion failed"); }
        async Task<JsonElement> Send(HttpMethod method, string path, string? extra = null, HttpStatusCode expected = HttpStatusCode.OK)
        {
            using var request = new HttpRequestMessage(method, path);
            if (extra != null) request.Content = new StringContent("{" + fields + extra + "}", Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);
            Check(response.StatusCode == expected);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (expected == HttpStatusCode.BadRequest) Check(doc.RootElement.GetProperty("errors").EnumerateObject().Any());
            return doc.RootElement.Clone();
        }
        await test("effective areas prioritize overrides, preserve zero and unknown", () =>
        {
            var resolver = typeof(UrbanSketch.Financial.Api.Models.FinancialModel).Assembly.GetType("UrbanSketch.Financial.Api.Models.ConstructionAreaResolver");
            Check(resolver != null);
            var method = resolver!.GetMethod("Resolve");
            var model = new UrbanSketch.Financial.Api.Models.FinancialModel();
            var concept = new UrbanSketch.Financial.Api.Models.ConceptVariant { ResidentialArea = 1000, CommercialArea = 200 };
            object Resolve() => method!.Invoke(null, new object[] { model, concept })!;
            decimal? Area(object result, string property) => (decimal?)result.GetType().GetProperty(property)!.GetValue(result);
            Check(Area(Resolve(), "ResidentialArea") == null && Area(Resolve(), "CommercialArea") == null);
            concept.ConstructionResidentialArea = 50; concept.ConstructionCommercialArea = 60;
            Check(Area(Resolve(), "ResidentialArea") == 50 && Area(Resolve(), "CommercialArea") == 60);
            model.GetType().GetProperty("ConstructionResidentialAreaOverride")!.SetValue(model, 0m);
            model.GetType().GetProperty("ConstructionCommercialAreaOverride")!.SetValue(model, 123m);
            Check(Area(Resolve(), "ResidentialArea") == 0 && Area(Resolve(), "CommercialArea") == 123);
            return Task.CompletedTask;
        });
        string path = $"/api/financial-models/{first}";
        await test("areas absent on legacy create are null", async () => Check((await Send(HttpMethod.Get, path)).GetProperty(r).ValueKind == JsonValueKind.Null));
        await test("PUT saves two precise areas and GET retrieves them", async () =>
        {
            await Send(HttpMethod.Put, path, $",\"{r}\":1000.12,\"{c}\":200.34");
            var saved = await Send(HttpMethod.Get, path);
            Check(saved.GetProperty(r).GetDecimal() == 1000.12m && saved.GetProperty(c).GetDecimal() == 200.34m);
        });
        await test("legacy PUT preserves both omitted areas", async () =>
        {
            var saved = await Send(HttpMethod.Put, path, "");
            Check(saved.GetProperty(r).GetDecimal() == 1000.12m && saved.GetProperty(c).GetDecimal() == 200.34m);
        });
        await test("invalid PUT never partially updates areas", async () =>
        {
            await Send(HttpMethod.Put, path, $",\"{r}\":500,\"{c}\":-1", HttpStatusCode.BadRequest);
            var saved = await Send(HttpMethod.Get, path);
            Check(saved.GetProperty(r).GetDecimal() == 1000.12m && saved.GetProperty(c).GetDecimal() == 200.34m);
        });
        await test("duplicate area properties are rejected", async () =>
            await Send(HttpMethod.Put, path, $",\"{r}\":500,\"{r}\":null", HttpStatusCode.BadRequest));
        await test("explicit null clears only supplied area", async () =>
        {
            var saved = await Send(HttpMethod.Put, path, $",\"{r}\":null");
            Check(saved.GetProperty(r).ValueKind == JsonValueKind.Null && saved.GetProperty(c).GetDecimal() == 200.34m);
        });
        await test("create zero and maximum exact area, get and list", async () =>
        {
            var created = await Send(HttpMethod.Post, "/api/financial-models", $",\"conceptVariantId\":\"a18d9b8a-3d42-4b92-b7e0-095b3e1a0102\",\"{r}\":0,\"{c}\":9999999999999999.99", HttpStatusCode.Created);
            var saved = await Send(HttpMethod.Get, $"/api/financial-models/{created.GetProperty("id").GetGuid()}");
            Check(saved.GetProperty(r).GetDecimal() == 0 && saved.GetProperty(c).GetDecimal() == 9999999999999999.99m);
            var list = await Send(HttpMethod.Get, "/api/concepts/a18d9b8a-3d42-4b92-b7e0-095b3e1a0102/financial-models");
            Check(list.EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid() && x.GetProperty(c).GetDecimal() == 9999999999999999.99m));
        });
        foreach (var field in new[] { r, c })
            foreach (var raw in new[] { "-1", "1.001", "10000000000000000", "1e-100", "1.00000000000000000000000000001", "\"NaN\"", "NaN", "Infinity", "true", "[]", "{}", "\"100\"" })
                await test($"reject area {field}={raw} on POST/PUT", async () =>
                {
                    await Send(HttpMethod.Put, path, $",\"{field}\":{raw}", HttpStatusCode.BadRequest);
                    await Send(HttpMethod.Post, "/api/financial-models", $",\"conceptVariantId\":\"a18d9b8a-3d42-4b92-b7e0-095b3e1a0101\",\"{field}\":{raw}", HttpStatusCode.BadRequest);
                });
        await test("create nullable area and exact scientific area", async () =>
        {
            var saved = await Send(HttpMethod.Post, "/api/financial-models", $",\"conceptVariantId\":\"a18d9b8a-3d42-4b92-b7e0-095b3e1a0102\",\"{r}\":null,\"{c}\":1.2300e2", HttpStatusCode.Created);
            Check(saved.GetProperty(r).ValueKind == JsonValueKind.Null && saved.GetProperty(c).GetDecimal() == 123m);
        });
        await test("neighbor model and source TEP remain unchanged", async () =>
        {
            Check((await Send(HttpMethod.Get, $"/api/financial-models/{second}")).GetProperty(c).ValueKind == JsonValueKind.Null);
            await using var db = context();
            Check(await db.ConceptVariants.AllAsync(x => x.ConstructionResidentialArea == null && x.ConstructionCommercialArea == null));
        });
        await test("clear second area", async () => Check((await Send(HttpMethod.Put, path, $",\"{c}\":null")).GetProperty(c).ValueKind == JsonValueKind.Null));
    }
}
