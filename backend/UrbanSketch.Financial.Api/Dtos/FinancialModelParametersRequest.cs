using System.Text.Json.Serialization;
using UrbanSketch.Financial.Api.Validation;

namespace UrbanSketch.Financial.Api.Dtos;

// Nullable input distinguishes an omitted parameter from a legitimate zero.
public abstract record FinancialModelParametersRequest
{
    private decimal? constructionResidentialAreaOverride;
    private decimal? constructionCommercialAreaOverride;

    [JsonIgnore]
    public bool HasConstructionResidentialAreaOverride { get; private set; }
    [JsonIgnore]
    public bool HasConstructionCommercialAreaOverride { get; private set; }

    [JsonConverter(typeof(ConstructionAreaJsonConverter))]
    public decimal? ConstructionResidentialAreaOverride
    {
        get => constructionResidentialAreaOverride;
        init { constructionResidentialAreaOverride = value; HasConstructionResidentialAreaOverride = true; }
    }
    [JsonConverter(typeof(ConstructionAreaJsonConverter))]
    public decimal? ConstructionCommercialAreaOverride
    {
        get => constructionCommercialAreaOverride;
        init { constructionCommercialAreaOverride = value; HasConstructionCommercialAreaOverride = true; }
    }

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
