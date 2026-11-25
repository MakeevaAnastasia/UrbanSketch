namespace UrbanSketch.Financial.Api.Models;

public sealed record EffectiveConstructionAreas(decimal? ResidentialArea, decimal? CommercialArea);

public static class ConstructionAreaResolver
{
    // Null is unknown; explicit zero takes precedence. Saleable areas are never a fallback.
    public static EffectiveConstructionAreas Resolve(FinancialModel model, ConceptVariant concept) => new(
        model.ConstructionResidentialAreaOverride ?? concept.ConstructionResidentialArea,
        model.ConstructionCommercialAreaOverride ?? concept.ConstructionCommercialArea);
}
