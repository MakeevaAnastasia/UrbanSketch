using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Dtos;
using UrbanSketch.Financial.Api.Models;
using UrbanSketch.Financial.Api.Validation;

namespace UrbanSketch.Financial.Api.Endpoints;

public static class FinancialModelsEndpoints
{
    public static IEndpointRouteBuilder MapFinancialModelsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var models = endpoints.MapGroup("/api/financial-models");
        models.MapPost("", CreateAsync).WithName("CreateFinancialModel");
        models.MapGet("/{id:guid}", GetAsync).WithName("GetFinancialModel");
        models.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateFinancialModel");
        models.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteFinancialModel");
        endpoints.MapGet("/api/concepts/{id:guid}/financial-models", ListAsync).WithName("GetConceptFinancialModels");
        return endpoints;
    }

    private static async Task<Results<Created<FinancialModelResponse>, ValidationProblem, NotFound>> CreateAsync(
        HttpRequest request, FinancialDbContext db, CancellationToken cancellationToken)
    {
        var (input, errors) = await FinancialModelValidation.ReadAsync<CreateFinancialModelRequest>(request, cancellationToken);
        if (errors.Count != 0) return TypedResults.ValidationProblem(errors);
        var conceptId = input!.ConceptVariantId!.Value;
        if (!await db.ConceptVariants.AsNoTracking().AnyAsync(v => v.Id == conceptId, cancellationToken))
            return TypedResults.NotFound();
        var model = new FinancialModel { ConceptVariantId = conceptId };
        FinancialModelValidation.Apply(input, model);
        db.FinancialModels.Add(model);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // The concept can disappear between the existence check and INSERT.
            return TypedResults.NotFound();
        }
        return TypedResults.Created($"/api/financial-models/{model.Id}", FinancialModelResponseMapping.FromEntity(model));
    }

    private static async Task<Results<Ok<FinancialModelResponse>, NotFound>> GetAsync(
        Guid id, FinancialDbContext db, CancellationToken cancellationToken)
    {
        var model = await db.FinancialModels.AsNoTracking().Where(m => m.Id == id)
            .Select(FinancialModelResponseMapping.Projection).SingleOrDefaultAsync(cancellationToken);
        return model == null ? TypedResults.NotFound() : TypedResults.Ok(model);
    }

    private static async Task<Results<Ok<List<FinancialModelResponse>>, NotFound>> ListAsync(
        Guid id, FinancialDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.ConceptVariants.AsNoTracking().AnyAsync(v => v.Id == id, cancellationToken))
            return TypedResults.NotFound();
        var models = await db.FinancialModels.AsNoTracking().Where(m => m.ConceptVariantId == id)
            .OrderBy(m => m.Id).Select(FinancialModelResponseMapping.Projection).ToListAsync(cancellationToken);
        return TypedResults.Ok(models);
    }

    private static async Task<Results<Ok<FinancialModelResponse>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, HttpRequest request, FinancialDbContext db, CancellationToken cancellationToken)
    {
        var model = await db.FinancialModels.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (model == null) return TypedResults.NotFound();
        var (input, errors) = await FinancialModelValidation.ReadAsync<UpdateFinancialModelRequest>(request, cancellationToken);
        if (errors.Count != 0) return TypedResults.ValidationProblem(errors);
        FinancialModelValidation.Apply(input!, model);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return TypedResults.NotFound(); }
        return TypedResults.Ok(FinancialModelResponseMapping.FromEntity(model));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, FinancialDbContext db, CancellationToken cancellationToken)
    {
        var model = await db.FinancialModels.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (model == null) return TypedResults.NotFound();
        db.FinancialModels.Remove(model);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return TypedResults.NotFound(); }
        return TypedResults.NoContent();
    }
}
