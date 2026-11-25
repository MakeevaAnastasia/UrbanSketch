using System.Linq.Expressions;
using UrbanSketch.Financial.Api.Models;

namespace UrbanSketch.Financial.Api.Dtos;

public sealed record FinancialModelResponse(
    Guid Id,
    Guid ConceptVariantId,
    decimal ResidentialConstructionCost,
    decimal CommercialConstructionCost,
    decimal ParkingCost,
    decimal ResidentialSalePrice,
    decimal CommercialSalePrice,
    decimal ParkingSalePrice,
    decimal LandCost,
    decimal AdditionalCosts,
    int ProjectDuration,
    decimal? TotalCosts,
    decimal? TotalRevenue,
    decimal? Profit,
    decimal? Roi,
    decimal? SalesProfitability,
    decimal? ConstructionResidentialAreaOverride,
    decimal? ConstructionCommercialAreaOverride);

internal static class FinancialModelResponseMapping
{
    public static readonly Expression<Func<FinancialModel, FinancialModelResponse>> Projection = m =>
        new FinancialModelResponse(m.Id, m.ConceptVariantId,
            m.ResidentialConstructionCost, m.CommercialConstructionCost, m.ParkingCost,
            m.ResidentialSalePrice, m.CommercialSalePrice, m.ParkingSalePrice,
            m.LandCost, m.AdditionalCosts, m.ProjectDuration,
            m.TotalCosts, m.TotalRevenue, m.Profit, m.Roi, m.SalesProfitability,
            m.ConstructionResidentialAreaOverride, m.ConstructionCommercialAreaOverride);

    private static readonly Func<FinancialModel, FinancialModelResponse> Map = Projection.Compile();
    public static FinancialModelResponse FromEntity(FinancialModel model)
    {
        var response = Map(model);
        return Calculations.SavedCalculation.IsCurrent(model) ? response : response with
        { TotalCosts=null, TotalRevenue=null, Profit=null, Roi=null, SalesProfitability=null };
    }
}
