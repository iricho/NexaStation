using System.Text;
using System.Text.RegularExpressions;
using NexaStation.Domain;

namespace NexaStation.Providers.Emulation.Launching;

public sealed partial class LaunchTemplateRenderer
{
    private static readonly HashSet<string> Variables = new(StringComparer.Ordinal)
    {
        "EMULATOR_PATH", "GAME_PATH", "GAME_DIRECTORY", "GAME_FILENAME", "GAME_FILENAME_NO_EXTENSION",
        "PLATFORM_ID", "GAME_IDENTIFIER", "CORE_PATH", "USER_DATA_DIRECTORY",
    };

    public static IReadOnlyList<string> RenderArguments(string template, EmulatorLaunchRequest request)
    {
        Validate(template);
        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["EMULATOR_PATH"] = request.Installation.ExecutablePath,
            ["GAME_PATH"] = request.Game.GamePath,
            ["GAME_DIRECTORY"] = Path.GetDirectoryName(request.Game.GamePath) ?? string.Empty,
            ["GAME_FILENAME"] = Path.GetFileName(request.Game.GamePath),
            ["GAME_FILENAME_NO_EXTENSION"] = Path.GetFileNameWithoutExtension(request.Game.GamePath),
            ["PLATFORM_ID"] = request.Platform.Id,
            ["GAME_IDENTIFIER"] = request.Game.GameIdentifier ?? string.Empty,
            ["CORE_PATH"] = request.Core?.CorePath ?? string.Empty,
            ["USER_DATA_DIRECTORY"] = request.Installation.UserDataDirectory ?? string.Empty,
        };

        List<string> result = [];
        foreach (string raw in Tokenize(template))
        {
            string rendered = VariableRegex().Replace(raw, match => values[match.Groups[1].Value]);
            if (string.Equals(rendered, request.Installation.ExecutablePath, StringComparison.OrdinalIgnoreCase) && result.Count == 0) continue;
            if (rendered.Length > 0) result.Add(rendered);
        }
        if (request.ExtraArguments is not null) result.AddRange(request.ExtraArguments.Where(argument => !string.IsNullOrWhiteSpace(argument)));
        return result;
    }

    public static void Validate(string template)
    {
        if (string.IsNullOrWhiteSpace(template)) throw new ArgumentException("An arguments template is required.", nameof(template));
        if (template.Contains('&') || template.Contains(';') || template.Contains('|') || template.Contains('\r') || template.Contains('\n'))
            throw new ArgumentException("Shell chaining and control characters are not supported.", nameof(template));
        foreach (Match match in VariableRegex().Matches(template))
            if (!Variables.Contains(match.Groups[1].Value)) throw new ArgumentException($"Unknown template variable '{{{match.Groups[1].Value}}}'.", nameof(template));
        string remainder = VariableRegex().Replace(template, string.Empty);
        if (remainder.Contains('{') || remainder.Contains('}')) throw new ArgumentException("The launch template contains an invalid variable.", nameof(template));
    }

    public static IReadOnlyList<string> Tokenize(string value)
    {
        List<string> result = [];
        StringBuilder current = new();
        bool quoted = false;
        char quote = '\0';
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if ((character == '"' || character == '\'') && (!quoted || quote == character))
            {
                quoted = !quoted;
                quote = quoted ? character : '\0';
                continue;
            }
            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(character);
        }
        if (quoted) throw new ArgumentException("The launch template contains an unmatched quote.", nameof(value));
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    [GeneratedRegex(@"\{([A-Z_]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariableRegex();
}
