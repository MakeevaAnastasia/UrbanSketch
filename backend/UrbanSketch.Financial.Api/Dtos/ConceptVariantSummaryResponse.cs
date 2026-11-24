namespace UrbanSketch.Financial.Api.Dtos;

public sealed record ConceptVariantSummaryResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    decimal ResidentialArea,
    decimal CommercialArea,
    int ParkingSpaces);
