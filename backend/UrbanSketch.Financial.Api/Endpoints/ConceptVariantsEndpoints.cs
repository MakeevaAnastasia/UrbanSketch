using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Dtos;

namespace UrbanSketch.Financial.Api.Endpoints;

public static class ConceptVariantsEndpoints
{
    public static IEndpointRouteBuilder MapConceptVariantsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/concepts/{id:guid}", GetConceptAsync).WithName("GetConcept");
        return endpoints;
    }

    private static async Task<Results<Ok<ConceptVariantDetailResponse>, NotFound>> GetConceptAsync(
        Guid id, FinancialDbContext db, CancellationToken cancellationToken)
    {
        var concept = await db.ConceptVariants.AsNoTracking().Where(v => v.Id == id)
            .Select(v => new ConceptVariantDetailResponse(
                v.Id, v.ProjectId, v.Name, v.ResidentialArea, v.CommercialArea,
                v.ConstructionResidentialArea, v.ConstructionCommercialArea, v.ParkingSpaces,
                v.ResidentsCount, v.BuildingsCount, v.MaxFloors, v.SocialAreas,
                v.PreschoolPlaces, v.SchoolPlaces))
            .SingleOrDefaultAsync(cancellationToken);
        return concept == null ? TypedResults.NotFound() : TypedResults.Ok(concept);
    }
}
