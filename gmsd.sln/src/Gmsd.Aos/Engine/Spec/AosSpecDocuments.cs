namespace Gmsd.Aos.Engine.Spec;

/// <summary>
/// Shared, minimal spec-layer documents written under <c>.aos/spec/**</c>.
/// These are intentionally small and schema-light for early milestones.
/// </summary>
internal sealed record ProjectSpecDocument(int SchemaVersion, ProjectSpec Project);

internal sealed record ProjectSpec(string Name, string Description);

internal sealed record RoadmapSpecDocument(int SchemaVersion, RoadmapSpec Roadmap);

internal sealed record RoadmapSpec(string Title, IReadOnlyList<RoadmapItemSpec> Items);

internal sealed record RoadmapItemSpec(string Id, string Title, string Kind);

/// <summary>
/// Catalog index contract for spec artifacts (IDs only).
/// </summary>
internal sealed record CatalogIndexDocument(int SchemaVersion, IReadOnlyList<string> Items);

