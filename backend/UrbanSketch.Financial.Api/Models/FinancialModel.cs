using Microsoft.EntityFrameworkCore;

namespace UrbanSketch.Financial.Api.Models;

public class FinancialModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConceptVariantId { get; set; }

    // Construction costs and sale prices are per square metre or per parking space.
    [Precision(20, 2)]
    public decimal ResidentialConstructionCost { get; set; }

    [Precision(20, 2)]
    public decimal CommercialConstructionCost { get; set; }

    [Precision(20, 2)]
    public decimal ParkingCost { get; set; }

    [Precision(20, 2)]
    public decimal ResidentialSalePrice { get; set; }

    [Precision(20, 2)]
    public decimal CommercialSalePrice { get; set; }

    [Precision(20, 2)]
    public decimal ParkingSalePrice { get; set; }

    [Precision(20, 2)]
    public decimal LandCost { get; set; }

    [Precision(20, 2)]
    public decimal AdditionalCosts { get; set; }

    // Duration in years, as specified in sprint 1.
    public int ProjectDuration { get; set; }

    // Stored results remain unknown until a calculation is performed.
    [Precision(20, 2)]
    public decimal? TotalCosts { get; set; }

    [Precision(20, 2)]
    public decimal? TotalRevenue { get; set; }

    [Precision(20, 2)]
    public decimal? Profit { get; set; }

    // Percentages; null also represents an undefined result for a zero denominator.
    [Precision(12, 4)]
    public decimal? Roi { get; set; }

    [Precision(12, 4)]
    public decimal? SalesProfitability { get; set; }

    public ConceptVariant ConceptVariant { get; set; } = null!;

    public ICollection<CashFlow> CashFlows { get; set; } = new List<CashFlow>();
}
