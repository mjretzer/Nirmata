namespace Gmsd.Aos.Engine.ExecutePlan;

internal sealed class ExecutePlanPlanLoadException : Exception
{
    public ExecutePlanPlanLoadException(string message)
        : base(message)
    {
    }

    public ExecutePlanPlanLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

