using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Models;
using Npgsql;

internal static class CalculationApiTests
{
    public static async Task RunAsync(Func<string,Func<Task>,Task> test, string connection, bool postgres = false)
    {
        FinancialDbContext Context() => postgres
            ? new FinancialDbContext(new DbContextOptionsBuilder<FinancialDbContext>().UseNpgsql(connection).Options)
            : new SqliteTestContext(new DbContextOptionsBuilder<FinancialDbContext>().UseSqlite(connection).Options);
        var project = new Project { Name="Calculation test only" };
        var concept = new ConceptVariant { Project=project, Name="Calculation fixture", ResidentialArea=1000, CommercialArea=200, ParkingSpaces=20 };
        await using(var db=Context()) { db.ConceptVariants.Add(concept); await db.SaveChangesAsync(); }
        await using var host=await ApiTestHost.StartAsync(connection, contextFactory:Context);
        var client=host.Client;
        const string parameters="\"residentialConstructionCost\":100000,\"commercialConstructionCost\":80000,\"parkingCost\":500000,\"residentialSalePrice\":200000,\"commercialSalePrice\":150000,\"parkingSalePrice\":1000000,\"landCost\":14000000,\"additionalCosts\":10000000,\"projectDuration\":3,\"constructionResidentialAreaOverride\":1000,\"constructionCommercialAreaOverride\":200";
        static void Check(bool ok) { if(!ok) throw new TestFailure("Calculation API assertion failed"); }
        async Task<JsonElement> Send(HttpMethod method,string path,string? body=null,int status=200)
        {
            using var request=new HttpRequestMessage(method,path);
            if(body!=null) request.Content=new StringContent(body,Encoding.UTF8,"application/json");
            using var response=await client.SendAsync(request);
            if((int)response.StatusCode!=status) throw new TestFailure($"Expected {status}, got {(int)response.StatusCode}");
            var raw=await response.Content.ReadAsStringAsync();
            return raw.Length==0?default:JsonDocument.Parse(raw).RootElement.Clone();
        }
        string Body(string p=parameters)=>"{"+p+"}";
        async Task<Guid> Create()=> (await Send(HttpMethod.Post,"/api/financial-models","{\"conceptVariantId\":\""+concept.Id+"\","+parameters+"}",201)).GetProperty("id").GetGuid();
        Guid id=Guid.Empty;
        string Path()=>"/api/financial-models/"+id;
        Task<JsonElement> Calculate(int status=200)=>Send(HttpMethod.Post,Path()+"/calculate",status:status);
        Task<JsonElement> Get()=>Send(HttpMethod.Get,Path()+"/calculation");
        try
        {
            await test("fingerprint ignores culture/decimal scale and detects algorithm/input changes",()=>{
                var m=new FinancialModel { ConstructionResidentialAreaOverride=1.0m };
                var c=new ConceptVariant { ResidentialArea=1000m };
                var previous=System.Globalization.CultureInfo.CurrentCulture;
                try {
                    var hash=UrbanSketch.Financial.Api.Calculations.CalculationFingerprint.Compute(m,c);
                    System.Globalization.CultureInfo.CurrentCulture=new("ru-RU");m.ConstructionResidentialAreaOverride=1.00m;
                    Check(UrbanSketch.Financial.Api.Calculations.CalculationFingerprint.Compute(m,c)==hash);
                    c.ResidentialArea=1001;Check(UrbanSketch.Financial.Api.Calculations.CalculationFingerprint.Compute(m,c)!=hash);
                    Check(UrbanSketch.Financial.Api.Calculations.CalculationFingerprint.AlgorithmVersion.Length>0);
                } finally { System.Globalization.CultureInfo.CurrentCulture=previous; }
                return Task.CompletedTask;
            });
            id=await Create();
            await test("calculation GET initially not_calculated",async()=>Check((await Get()).GetProperty("state").GetString()=="not_calculated"));
            await test("HTTP TZ calculate persists complete components and indicators",async()=>{
                var response=await Calculate();var r=response.GetProperty("result");
                Check(response.GetProperty("state").GetString()=="current" && response.GetProperty("isCurrent").GetBoolean());
                Check(r.GetProperty("totalCosts").GetDecimal()==150000000m && r.GetProperty("totalRevenue").GetDecimal()==250000000m && r.GetProperty("profit").GetDecimal()==100000000m && r.GetProperty("roi").GetDecimal()==66.6667m);
                Check(r.GetProperty("costs").GetProperty("residential").GetDecimal()==100000000m && r.GetProperty("revenue").GetProperty("commercial").GetDecimal()==30000000m);
                await using var db=Context();var saved=await db.FinancialModels.SingleAsync(m=>m.Id==id);Check(saved.TotalCosts==150000000m && saved.Profit==100000000m && saved.LandCost==14000000m);
            });
            await test("GET reopens saved snapshot and repeat POST is deterministic",async()=>{var first=(await Get()).GetRawText();Check((await Calculate()).GetRawText()==first && (await Get()).GetRawText()==first);});
            await test("new endpoints missing id return 404",async()=>{foreach(var suffix in new[]{"/calculate","/calculation"})await Send(suffix=="/calculate"?HttpMethod.Post:HttpMethod.Get,"/api/financial-models/"+Guid.NewGuid()+suffix,status:404);});
            await test("calculate rejects client arithmetic body",async()=>{await Send(HttpMethod.Post,Path()+"/calculate","{\"profit\":1}",400);});
            await test("PUT invalidates calculation, CRUD masks old totals, recalculate updates",async()=>{
                await Send(HttpMethod.Put,Path(),Body(parameters.Replace("\"residentialSalePrice\":200000","\"residentialSalePrice\":210000")));
                Check((await Get()).GetProperty("state").GetString()=="stale");
                Check((await Send(HttpMethod.Get,Path())).GetProperty("profit").ValueKind==JsonValueKind.Null);
                Check((await Calculate()).GetProperty("result").GetProperty("totalRevenue").GetDecimal()==260000000m);
            });
            await test("changing construction override invalidates and recalculates",async()=>{await Send(HttpMethod.Put,Path(),Body(parameters.Replace("Override\":1000","Override\":1200")));Check((await Get()).GetProperty("state").GetString()=="stale");Check((await Calculate()).GetProperty("result").GetProperty("totalCosts").GetDecimal()==170000000m);});
            await test("cleared override yields incomplete known revenue without partial persistence",async()=>{
                await Send(HttpMethod.Put,Path(),Body(parameters.Replace("Override\":1000","Override\":null")));
                var r=(await Calculate()).GetProperty("result");Check(r.GetProperty("totalRevenue").GetDecimal()==250000000m && r.GetProperty("totalCosts").ValueKind==JsonValueKind.Null && r.GetProperty("missingInputs").GetArrayLength()==1);
                Check((await Get()).GetProperty("state").GetString()=="stale");
            });
            await test("source TEP change invalidates saved hash",async()=>{
                await Send(HttpMethod.Put,Path(),Body());await Calculate();await using(var db=Context()){var c=await db.ConceptVariants.SingleAsync(c=>c.Id==concept.Id);c.ResidentialArea=1100;await db.SaveChangesAsync();}
                Check((await Get()).GetProperty("state").GetString()=="stale");Check((await Calculate()).GetProperty("result").GetProperty("totalRevenue").GetDecimal()==270000000m);
                await using var restore=Context();var source=await restore.ConceptVariants.SingleAsync(c=>c.Id==concept.Id);source.ResidentialArea=1000;await restore.SaveChangesAsync();
            });
            foreach(var (name,p,coefficient) in new[]{("zero costs",parameters.Replace(":100000,",":0,").Replace(":80000,",":0,").Replace(":500000,",":0,").Replace(":14000000,",":0,").Replace(":10000000,",":0,"),"roi"),("zero revenue",parameters.Replace(":200000,",":0,").Replace(":150000,",":0,").Replace(":1000000,",":0,"),"salesProfitability")})
                await test(name+" stores null ratio as current",async()=>{await Send(HttpMethod.Put,Path(),Body(p));var r=await Calculate();Check(r.GetProperty("state").GetString()=="current" && r.GetProperty("result").GetProperty(coefficient).ValueKind==JsonValueKind.Null);});
            foreach(var (name,p) in new[]{("decimal overflow",parameters.Replace("Override\":1000","Override\":9999999999999999.99").Replace("\"residentialConstructionCost\":100000","\"residentialConstructionCost\":999999999999999999.99")),("storage range",parameters.Replace("\"landCost\":14000000","\"landCost\":999999999999999999.99")),("storage scale",parameters.Replace("Override\":1000","Override\":0.01").Replace("\"residentialConstructionCost\":100000","\"residentialConstructionCost\":0.01"))})
                await test(name+" is structured 422 with no partial write",async()=>{
                    await Send(HttpMethod.Put,Path(),Body(p));await using var db=Context();var before=await db.FinancialModels.AsNoTracking().SingleAsync(m=>m.Id==id);var r=await Calculate(422);Check(r.GetProperty("state").GetString()=="error");
                    var after=await db.FinancialModels.AsNoTracking().SingleAsync(m=>m.Id==id);Check(before.Profit==after.Profit && before.TotalCosts==after.TotalCosts && before.CalculationInputHash==after.CalculationInputHash && before.CalculationSnapshot==after.CalculationSnapshot);
                });
            await test("another model is independent and unknown calculation stays absent",async()=>{var other=await Create();Check((await Send(HttpMethod.Get,"/api/financial-models/"+other+"/calculation")).GetProperty("state").GetString()=="not_calculated");});
            await test("legacy list masks stale values and nonexistent snapshot is not recalculated on GET",async()=>{
                var list=await Send(HttpMethod.Get,"/api/concepts/"+concept.Id+"/financial-models");
                Check(list.EnumerateArray().Single(x=>x.GetProperty("id").GetGuid()==id).GetProperty("profit").ValueKind==JsonValueKind.Null);
                await using var db=Context();var model=await db.FinancialModels.SingleAsync(m=>m.Id==id);Check(model.CalculationInputHash!=null);
            });
            if(postgres) await test("PostgreSQL concurrent model/concept write yields 409 without persistence",async()=>{
                foreach(var table in new[]{"financial_models","concept_variants"}) {
                    await using var blocker=new NpgsqlConnection(connection);await blocker.OpenAsync();await using var tx=await blocker.BeginTransactionAsync();
                    await using var cmd=new NpgsqlCommand($"SELECT id FROM {table} WHERE id=@id FOR UPDATE",blocker,tx);cmd.Parameters.AddWithValue("id",table=="financial_models"?id:concept.Id);await cmd.ExecuteScalarAsync();
                    await Calculate(409);await tx.RollbackAsync();
                }
            });
            await test("deleting model removes access to calculation",async()=>{await Send(HttpMethod.Delete,Path(),status:204);await Send(HttpMethod.Get,Path()+"/calculation",status:404);});
        }
        finally {await using var db=Context();var owned=await db.Projects.SingleAsync(p=>p.Id==project.Id);db.Projects.Remove(owned);await db.SaveChangesAsync();}
    }
}
