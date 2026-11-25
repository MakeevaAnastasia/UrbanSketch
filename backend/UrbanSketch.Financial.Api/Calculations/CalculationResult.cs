namespace UrbanSketch.Financial.Api.Calculations;

public enum CalculationStatus { Complete, Incomplete, Failed }

public sealed record CalculationIssue(string Field, string Code, string Message);
public sealed record CostBreakdown(decimal? Residential, decimal? Commercial, decimal? Parking, decimal? Land, decimal? Additional);
public sealed record RevenueBreakdown(decimal? Residential, decimal? Commercial, decimal? Parking);

public sealed record CalculationResult(
    CalculationStatus Status,
    CostBreakdown Costs,
    RevenueBreakdown Revenue,
    decimal? TotalCosts,
    decimal? TotalRevenue,
    decimal? Profit,
    decimal? Roi,
    decimal? SalesProfitability,
    IReadOnlyList<string> MissingInputs,
    IReadOnlyList<CalculationIssue> Errors,
    IReadOnlyList<CalculationIssue> PersistenceIssues)
{
    public bool CanPersistToCurrentSchema => Status == CalculationStatus.Complete && PersistenceIssues.Count == 0;
}
