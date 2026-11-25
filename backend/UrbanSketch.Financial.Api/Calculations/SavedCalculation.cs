using System.Text.Json;
using System.Text.Json.Serialization;
using UrbanSketch.Financial.Api.Models;

namespace UrbanSketch.Financial.Api.Calculations;

public sealed record CalculationResponse(string State, bool IsCurrent, CalculationResult? Result);

public static class SavedCalculation
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    public static bool IsCurrent(FinancialModel model) => model.CalculationInputHash != null && model.CalculationSnapshot != null
        && model.ConceptVariant != null && model.CalculationInputHash == CalculationFingerprint.Compute(model,model.ConceptVariant);

    public static CalculationResponse Read(FinancialModel model)
    {
        if (model.CalculationInputHash == null || model.CalculationSnapshot == null) return new("not_calculated",false,null);
        if (!IsCurrent(model)) return new("stale",false,null);
        return new("current",true,JsonSerializer.Deserialize<CalculationResult>(model.CalculationSnapshot,JsonOptions)
            ?? throw new InvalidOperationException("Invalid stored calculation snapshot."));
    }
}
