namespace UrbanSketch.Financial.Api.Data.Seed;

// Nullable input fields distinguish missing values from legitimate zeroes.
internal sealed class TepSeedDocument
{
    public Guid? Id { get; set; }
    public string? Name { get; set; }
    public List<TepSeedVariant?>? Variants { get; set; }
}

internal sealed class TepSeedVariant
{
    public Guid? Id { get; set; }
    public string? Name { get; set; }
    public decimal? ResidentialArea { get; set; }
    public decimal? CommercialArea { get; set; }
    public int? ParkingSpaces { get; set; }
    public decimal? ConstructionResidentialArea { get; set; }
    public decimal? ConstructionCommercialArea { get; set; }
    public int? ResidentsCount { get; set; }
    public int? BuildingsCount { get; set; }
    public int? MaxFloors { get; set; }
    public decimal? SocialAreas { get; set; }
    public int? PreschoolPlaces { get; set; }
    public int? SchoolPlaces { get; set; }
}
