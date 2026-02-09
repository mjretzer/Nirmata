namespace Gmsd.Aos.Engine.ExecutePlan;

internal sealed class ExecutePlanPlan
{
    public ExecutePlanPlan(int schemaVersion, IReadOnlyList<ExecutePlanPlanOutput> outputs)
    {
        SchemaVersion = schemaVersion;
        Outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    public int SchemaVersion { get; }

    public IReadOnlyList<ExecutePlanPlanOutput> Outputs { get; }
}

