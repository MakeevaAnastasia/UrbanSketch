using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Dtos;

namespace UrbanSketch.Financial.Api.Endpoints;

public static class ProjectsEndpoints
{
    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var projects = endpoints.MapGroup("/api/projects");
        projects.MapGet("", GetProjectsAsync).WithName("GetProjects");
        projects.MapGet("/{id:guid}", GetProjectAsync).WithName("GetProject");
        projects.MapGet("/{id:guid}/variants", GetVariantsAsync).WithName("GetProjectVariants");
        return endpoints;
    }

    private static async Task<Ok<List<ProjectResponse>>> GetProjectsAsync(
        FinancialDbContext db, CancellationToken cancellationToken)
    {
        var projects = await db.Projects.AsNoTracking()
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => new ProjectResponse(p.Id, p.Name, p.CreatedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(projects);
    }

    private static async Task<Results<Ok<ProjectResponse>, NotFound>> GetProjectAsync(
        Guid id, FinancialDbContext db, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new ProjectResponse(p.Id, p.Name, p.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        return project == null ? TypedResults.NotFound() : TypedResults.Ok(project);
    }

    private static async Task<Results<Ok<List<ConceptVariantSummaryResponse>>, NotFound>> GetVariantsAsync(
        Guid id, FinancialDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AsNoTracking().AnyAsync(p => p.Id == id, cancellationToken))
            return TypedResults.NotFound();

        var variants = await db.ConceptVariants.AsNoTracking().Where(v => v.ProjectId == id)
            .OrderBy(v => v.Name).ThenBy(v => v.Id)
            .Select(v => new ConceptVariantSummaryResponse(
                v.Id, v.ProjectId, v.Name, v.ResidentialArea, v.CommercialArea, v.ParkingSpaces))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(variants);
    }
}
