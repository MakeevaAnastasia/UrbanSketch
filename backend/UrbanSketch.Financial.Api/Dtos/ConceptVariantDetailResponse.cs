namespace UrbanSketch.Financial.Api.Dtos;

public sealed record ConceptVariantDetailResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    decimal ResidentialArea,
    decimal CommercialArea,
    decimal? ConstructionResidentialArea,
    decimal? ConstructionCommercialArea,
    int ParkingSpaces,
    int? ResidentsCount,
    int? BuildingsCount,
    int? MaxFloors,
    decimal? SocialAreas,
    int? PreschoolPlaces,
    int? SchoolPlaces);
