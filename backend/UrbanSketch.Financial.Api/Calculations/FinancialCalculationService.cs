using UrbanSketch.Financial.Api.Models;

namespace UrbanSketch.Financial.Api.Calculations;

/// <summary>Stateless calculations only: no entity mutation, database access or HTTP dependencies.</summary>
public sealed class FinancialCalculationService
{
    public CalculationResult Calculate(FinancialModel model, ConceptVariant concept)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(concept);
        var errors = new List<CalculationIssue>();
        var missing = new List<string>();
        var storage = new List<CalculationIssue>();
        var areas = ConstructionAreaResolver.Resolve(model, concept);

        decimal? Read(decimal? value, string field, decimal max)
        {
            if (value == null) { missing.Add(field); return null; }
            if (value < 0 || value > max || decimal.Round(value.Value, 2) != value.Value)
            {
                errors.Add(new(field, "invalidInput", "Ожидается неотрицательное точное значение в допустимом диапазоне с точностью до сотых."));
                return null;
            }
            return value;
        }
        decimal? Money(decimal value, string field) => Read(value, field, 999999999999999999.99m);
        decimal? Area(decimal? value, string field) => Read(value, field, 9999999999999999.99m);
        decimal? Compute(string field, Func<decimal> calculate)
        {
            try { return calculate(); }
            catch (OverflowException)
            {
                errors.Add(new(field, "decimalOverflow", "Результат не представим точно в decimal; расчёт этого показателя невозможен без потери точности."));
                return null;
            }
        }
        decimal? Product(string field, decimal? a, decimal? b) => a.HasValue && b.HasValue
            ? Compute(field, () => ExactDecimal.Multiply(a.Value, b.Value)) : null;
        decimal? Sum(string field, params decimal?[] values) => values.All(x => x.HasValue)
            ? Compute(field, () => ExactDecimal.Sum(values.Select(x => x!.Value).ToArray())) : null;

        var residentialArea = Area(areas.ResidentialArea, "constructionResidentialArea");
        var commercialArea = Area(areas.CommercialArea, "constructionCommercialArea");
        var saleResidentialArea = Area(concept.ResidentialArea, "residentialArea");
        var saleCommercialArea = Area(concept.CommercialArea, "commercialArea");
        decimal? parking = concept.ParkingSpaces;
        if (concept.ParkingSpaces < 0)
        {
            errors.Add(new("parkingSpaces", "invalidInput", "Количество машино-мест не может быть отрицательным."));
            parking = null;
        }

        var residentialCosts = Product("residentialCosts", residentialArea, Money(model.ResidentialConstructionCost, "residentialConstructionCost"));
        var commercialCosts = Product("commercialCosts", commercialArea, Money(model.CommercialConstructionCost, "commercialConstructionCost"));
        var parkingCosts = Product("parkingCosts", parking, Money(model.ParkingCost, "parkingCost"));
        var land = Money(model.LandCost, "landCost");
        var additional = Money(model.AdditionalCosts, "additionalCosts");
        var totalCosts = Sum("totalCosts", residentialCosts, commercialCosts, parkingCosts, land, additional);

        var residentialRevenue = Product("residentialRevenue", saleResidentialArea, Money(model.ResidentialSalePrice, "residentialSalePrice"));
        var commercialRevenue = Product("commercialRevenue", saleCommercialArea, Money(model.CommercialSalePrice, "commercialSalePrice"));
        var parkingRevenue = Product("parkingRevenue", parking, Money(model.ParkingSalePrice, "parkingSalePrice"));
        var totalRevenue = Sum("totalRevenue", residentialRevenue, commercialRevenue, parkingRevenue);
        var profit = Sum("profit", totalRevenue, totalCosts.HasValue ? -totalCosts.Value : null);
        decimal? Percentage(string field, decimal? denominator) => profit.HasValue && denominator.HasValue && denominator.Value != 0
            ? Compute(field, () => ExactDecimal.Percent(profit.Value, denominator.Value)) : null;
        var roi = Percentage("roi", totalCosts);
        var margin = Percentage("salesProfitability", totalRevenue);

        void CheckStorage(decimal? value, string field, decimal max, int scale)
        {
            if (!value.HasValue) return;
            if (value > max || value < -max)
                storage.Add(new(field, "storageRange", "Результат выходит за диапазон существующего PostgreSQL numeric-столбца."));
            if (decimal.Round(value.Value, scale) != value.Value)
                storage.Add(new(field, "storageScale", "Точное сохранение потребует большей дробной точности PostgreSQL; автоматическое округление запрещено."));
        }
        CheckStorage(totalCosts, "totalCosts", 999999999999999999.99m, 2);
        CheckStorage(totalRevenue, "totalRevenue", 999999999999999999.99m, 2);
        CheckStorage(profit, "profit", 999999999999999999.99m, 2);
        CheckStorage(roi, "roi", 99999999.9999m, 4);
        CheckStorage(margin, "salesProfitability", 99999999.9999m, 4);

        var status = errors.Count > 0 ? CalculationStatus.Failed : missing.Count > 0 ? CalculationStatus.Incomplete : CalculationStatus.Complete;
        return new(status, new(residentialCosts, commercialCosts, parkingCosts, land, additional),
            new(residentialRevenue, commercialRevenue, parkingRevenue), totalCosts, totalRevenue, profit, roi, margin,
            missing.AsReadOnly(), errors.AsReadOnly(), storage.AsReadOnly());
    }
}
