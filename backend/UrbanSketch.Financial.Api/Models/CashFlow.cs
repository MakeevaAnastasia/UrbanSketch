using Microsoft.EntityFrameworkCore;

namespace UrbanSketch.Financial.Api.Models;

public class CashFlow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FinancialModelId { get; set; }

    // Year number, starting at 1.
    public int PeriodNumber { get; set; }

    // Shares in percent (0–100), rather than fractions.
    [Precision(7, 4)]
    public decimal CostShare { get; set; }

    [Precision(7, 4)]
    public decimal RevenueShare { get; set; }

    // Stored results remain unknown until a calculation is performed.
    [Precision(20, 2)]
    public decimal? PeriodCosts { get; set; }

    [Precision(20, 2)]
    public decimal? PeriodRevenue { get; set; }

    [Precision(20, 2)]
    public decimal? NetCashFlow { get; set; }

    [Precision(20, 2)]
    public decimal? CumulativeCashFlow { get; set; }

    public FinancialModel FinancialModel { get; set; } = null!;
}
