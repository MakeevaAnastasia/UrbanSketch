namespace UrbanSketch.Financial.Api.Models;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConceptVariant> Variants { get; set; } = new List<ConceptVariant>();
}
