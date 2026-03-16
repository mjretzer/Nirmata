namespace nirmata.Aos.Public.Catalogs;

/// <summary>
/// Stable catalog of command identifiers understood by AOS.
/// </summary>
public static class CommandIds
{
    // Init group
    public const string Init = "init";

    // Status group
    public const string Status = "status";

    // Config group
    public const string ConfigGet = "config.get";
    public const string ConfigSet = "config.set";
    public const string ConfigList = "config.list";

    // Validate group
    public const string Validate = "validate";

    // Spec group
    public const string SpecList = "spec.list";
    public const string SpecShow = "spec.show";
    public const string SpecApply = "spec.apply";

    // State group
    public const string StateShow = "state.show";
    public const string StateDiff = "state.diff";

    // Run group
    public const string RunExecute = "run.execute";
    public const string RunList = "run.list";

    // Help group
    public const string Help = "help";

    // Progress group
    public const string ReportProgress = "core.report-progress";

    // History group
    public const string WriteHistory = "core.write-history";

    // Backlog group
    public const string Backlog = "core.backlog";
}

