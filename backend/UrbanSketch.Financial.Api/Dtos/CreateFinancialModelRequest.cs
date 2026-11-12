namespace UrbanSketch.Financial.Api.Dtos;

public sealed record CreateFinancialModelRequest : FinancialModelParametersRequest
{
    public Guid? ConceptVariantId { get; init; }
}
