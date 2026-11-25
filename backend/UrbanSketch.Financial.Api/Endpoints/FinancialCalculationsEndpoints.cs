using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UrbanSketch.Financial.Api.Calculations;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Models;

namespace UrbanSketch.Financial.Api.Endpoints;

public static class FinancialCalculationsEndpoints
{
    public static IEndpointRouteBuilder MapFinancialCalculationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/financial-models/{id:guid}/calculate",CalculateAsync);
        endpoints.MapGet("/api/financial-models/{id:guid}/calculation",GetAsync);
        return endpoints;
    }
    private static IResult Json(CalculationResponse response,int code=200) => Results.Json(response,SavedCalculation.JsonOptions,statusCode:code);

    private static async Task<IResult> GetAsync(Guid id,FinancialDbContext db,CancellationToken ct)
    {
        var model=await db.FinancialModels.AsNoTracking().Include(x=>x.ConceptVariant).SingleOrDefaultAsync(x=>x.Id==id,ct);
        return model==null ? Results.NotFound() : Json(SavedCalculation.Read(model));
    }

    private static async Task<IResult> CalculateAsync(Guid id,HttpRequest request,FinancialDbContext db,FinancialCalculationService calculator,CancellationToken ct)
    {
        // There are no client-supplied calculation parameters, including chunked request bodies.
        if(request.ContentLength is >0 || request.Headers.ContainsKey("Transfer-Encoding"))
            return Results.ValidationProblem(new Dictionary<string,string[]> { ["body"]=["Расчёт принимает только сохранённые параметры; тело запроса не допускается."] });
        IDbContextTransaction? transaction=null;
        try
        {
            transaction=await db.Database.BeginTransactionAsync(ct);
            FinancialModel? model;
            if(db.Database.IsNpgsql())
            {
                var conceptId=await db.FinancialModels.AsNoTracking().Where(m=>m.Id==id).Select(m=>(Guid?)m.ConceptVariantId).SingleOrDefaultAsync(ct);
                if(conceptId==null) return Results.NotFound();
                // Consistent lock order: concept before model. NOWAIT makes conflicts explicit.
                var concept=await db.ConceptVariants.FromSqlInterpolated($"SELECT * FROM concept_variants WHERE id={conceptId.Value} FOR UPDATE NOWAIT").SingleOrDefaultAsync(ct);
                model=await db.FinancialModels.FromSqlInterpolated($"SELECT * FROM financial_models WHERE id={id} FOR UPDATE NOWAIT").SingleOrDefaultAsync(ct);
                if(model==null || concept==null) return Results.NotFound();
                if(model.ConceptVariantId!=concept.Id) return Results.Conflict(new { message="Исходные данные изменились. Повторите расчёт." });
                model.ConceptVariant=concept;
            }
            else model=await db.FinancialModels.Include(x=>x.ConceptVariant).SingleOrDefaultAsync(x=>x.Id==id,ct);
            if(model==null) return Results.NotFound();
            var result=calculator.Calculate(model,model.ConceptVariant);
            if(result.Status==CalculationStatus.Incomplete) return Json(new("incomplete",false,result));
            if(result.Status==CalculationStatus.Failed || !result.CanPersistToCurrentSchema) return Json(new("error",false,result),422);
            model.TotalCosts=result.TotalCosts;model.TotalRevenue=result.TotalRevenue;model.Profit=result.Profit;
            model.Roi=result.Roi;model.SalesProfitability=result.SalesProfitability;
            model.CalculationInputHash=CalculationFingerprint.Compute(model,model.ConceptVariant);
            model.CalculationSnapshot=JsonSerializer.Serialize(result,SavedCalculation.JsonOptions);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Json(new("current",true,result));
        }
        catch(Exception ex) when(IsConcurrencyConflict(ex))
        { return Results.Problem(statusCode:409,title:"Конкурентное изменение данных. Повторите расчёт."); }
        finally { if(transaction!=null) await transaction.DisposeAsync(); }
    }
    // Npgsql's execution strategy can wrap lock conflicts in InvalidOperationException.
    private static bool IsConcurrencyConflict(Exception error)
    {
        for(Exception? issue=error; issue!=null; issue=issue.InnerException)
            if(issue is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
                return true;
        return false;
    }

}
