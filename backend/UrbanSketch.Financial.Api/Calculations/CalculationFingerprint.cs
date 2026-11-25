using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UrbanSketch.Financial.Api.Models;

namespace UrbanSketch.Financial.Api.Calculations;

public static class CalculationFingerprint
{
    public const string AlgorithmVersion = "financial-v1-percent4-away-exact";
    public static string Compute(FinancialModel m, ConceptVariant c)
    {
        var effective = ConstructionAreaResolver.Resolve(m,c);
        var values = new decimal?[] { m.ResidentialConstructionCost,m.CommercialConstructionCost,m.ParkingCost,
            m.ResidentialSalePrice,m.CommercialSalePrice,m.ParkingSalePrice,m.LandCost,m.AdditionalCosts,m.ProjectDuration,
            m.ConstructionResidentialAreaOverride,m.ConstructionCommercialAreaOverride,c.ConstructionResidentialArea,
            c.ConstructionCommercialArea,effective.ResidentialArea,effective.CommercialArea,c.ResidentialArea,c.CommercialArea,c.ParkingSpaces };
        var canonical = AlgorithmVersion + "|" + string.Join("|", values.Select(x => x?.ToString("G29",CultureInfo.InvariantCulture) ?? "null"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
