namespace Gmsd.Aos.Context.Packs;

public sealed record ContextPackDocument(
    int SchemaVersion,
    string PackId,
    string Mode,
    string DrivingId,
    ContextPackBudget Budget,
    ContextPackSummary Summary,
    IReadOnlyList<ContextPackEntryDocument> Entries);

public sealed record ContextPackBudget(
    int MaxBytes,
    int MaxItems);

public sealed record ContextPackSummary(
    int TotalBytes,
    int TotalItems);

public sealed record ContextPackEntryDocument(
    string ContractPath,
    string ContentType,
    string Content,
    string Sha256,
    int Bytes);

