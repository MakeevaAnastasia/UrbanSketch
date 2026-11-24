namespace UrbanSketch.Financial.Api.Dtos;

// Nullable input distinguishes an omitted parameter from a legitimate zero.
public abstract record FinancialModelParametersRequest
{
    public decimal? ResidentialConstructionCost { get; init; }
    public decimal? CommercialConstructionCost { get; init; }
    public decimal? ParkingCost { get; init; }
    public decimal? ResidentialSalePrice { get; init; }
    public decimal? CommercialSalePrice { get; init; }
    public decimal? ParkingSalePrice { get; init; }
    public decimal? LandCost { get; init; }
    public decimal? AdditionalCosts { get; init; }
    public int? ProjectDuration { get; init; }
}
