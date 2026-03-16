namespace nirmata.Aos.Engine.ExecutePlan;

internal sealed class ExecutePlanPlanOutput
{
    public ExecutePlanPlanOutput(string relativePath, string contentsUtf8)
    {
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        ContentsUtf8 = contentsUtf8 ?? throw new ArgumentNullException(nameof(contentsUtf8));
    }

    public string RelativePath { get; }

    public string ContentsUtf8 { get; }
}

