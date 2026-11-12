using Microsoft.EntityFrameworkCore;

namespace UrbanSketch.Financial.Api.Models;

public class ConceptVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    // Saleable areas in square metres, separate from construction areas.
    [Precision(18, 2)]
    public decimal ResidentialArea { get; set; }

    [Precision(18, 2)]
    public decimal CommercialArea { get; set; }

    // Null until supplied explicitly; importing saleable areas does not populate these.
    [Precision(18, 2)]
    public decimal? ConstructionResidentialArea { get; set; }

    [Precision(18, 2)]
    public decimal? ConstructionCommercialArea { get; set; }

    public int ParkingSpaces { get; set; }

    public int? ResidentsCount { get; set; }

    public int? BuildingsCount { get; set; }

    public int? MaxFloors { get; set; }

    [Precision(18, 2)]
    public decimal? SocialAreas { get; set; }

    public int? PreschoolPlaces { get; set; }

    public int? SchoolPlaces { get; set; }

    public Project Project { get; set; } = null!;

    public ICollection<FinancialModel> FinancialModels { get; set; } = new List<FinancialModel>();
}
