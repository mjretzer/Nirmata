using System.Text.RegularExpressions;

namespace Gmsd.Agents.Execution.Preflight;

public sealed class InputClassifier
{
    public Intent Classify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new Intent { Kind = IntentKind.Unknown, SideEffect = SideEffect.None, Confidence = 1.0 };
        }

        input = input.Trim();

        if (IsMatch(input, "^(hi|hello|yo|thanks|lol|hey|bye)$"))
        {
            return new Intent { Kind = IntentKind.SmallTalk, SideEffect = SideEffect.None, Confidence = 1.0 };
        }

        if (input.StartsWith("aos") || input.StartsWith("/"))
        {
            return new Intent { Kind = IntentKind.WorkflowCommand, SideEffect = SideEffect.Write, Confidence = 1.0 };
        }

        if (IsMatch(input, "^(help|commands|what can you do)$"))
        {
            return new Intent { Kind = IntentKind.Help, SideEffect = SideEffect.ReadOnly, Confidence = 1.0 };
        }

        if (IsMatch(input, "^(status|progress|where are we)$"))
        {
            return new Intent { Kind = IntentKind.Status, SideEffect = SideEffect.ReadOnly, Confidence = 1.0 };
        }

        if (IsMatch(input, "(create|plan|execute|run|verify|fix|pause|resume)"))
        {
            return new Intent { Kind = IntentKind.WorkflowFreeform, SideEffect = SideEffect.Write, Confidence = 0.8 };
        }

        return new Intent { Kind = IntentKind.Unknown, SideEffect = SideEffect.None, Confidence = 0.5 };
    }

    private static bool IsMatch(string input, string pattern)
    {
        return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
    }
}
