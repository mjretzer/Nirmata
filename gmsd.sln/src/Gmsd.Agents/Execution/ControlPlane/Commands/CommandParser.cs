namespace Gmsd.Agents.Execution.ControlPlane.Commands;

/// <summary>
/// Parses user input to extract slash commands with strict prefix rules.
/// Enforces that commands must start with '/' followed by a command name.
/// </summary>
public class CommandParser : ICommandParser
{
    private static readonly HashSet<string> CoreCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "help",
        "status",
        "run",
        "plan",
        "verify",
        "fix"
    };

    public ParsedCommand? TryParseCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.TrimStart();
        if (!trimmed.StartsWith('/'))
        {
            return null;
        }

        var parts = SplitCommandAndArgs(trimmed);
        if (parts.Count == 0)
        {
            return null;
        }

        var commandName = parts[0].Substring(1).ToLowerInvariant();
        if (string.IsNullOrEmpty(commandName))
        {
            return null;
        }

        var arguments = ParseArguments(parts.Skip(1).ToList());

        return new ParsedCommand
        {
            CommandName = commandName,
            Arguments = arguments,
            RawInput = input
        };
    }

    public bool IsCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return input.TrimStart().StartsWith('/');
    }

    /// <summary>
    /// Splits the input into command and arguments, respecting quoted strings.
    /// </summary>
    private static List<string> SplitCommandAndArgs(string input)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var escapeNext = false;
        var hadQuotes = false;

        foreach (var c in input)
        {
            if (escapeNext)
            {
                current.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                hadQuotes = true;
                continue;
            }

            if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0 || hadQuotes)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                    hadQuotes = false;
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0 || hadQuotes)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }

    /// <summary>
    /// Parses arguments into a dictionary, supporting key=value and positional args.
    /// </summary>
    private static Dictionary<string, object?> ParseArguments(List<string> args)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var positionalIndex = 0;

        foreach (var arg in args)
        {
            if (arg.Contains('='))
            {
                var kvp = arg.Split('=', 2);
                var key = kvp[0].Trim();
                var value = kvp.Length > 1 ? kvp[1].Trim() : null;
                result[key] = value;
            }
            else
            {
                result[$"arg{positionalIndex}"] = arg;
                positionalIndex++;
            }
        }

        return result;
    }
}
