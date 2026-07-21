using System.Text.RegularExpressions;
using NexaStation.TextInput.Sessions;

namespace NexaStation.TextInput.Validation;

public sealed record TextInputValidationResult(bool IsValid, string? Error = null);

public sealed class TextInputValidator
{
    private readonly HashSet<string> _allowedUrlSchemes = new(StringComparer.OrdinalIgnoreCase) { "http", "https" };

    public TextInputValidationResult Validate(TextInputRequest request, string text)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(text);
        if (!request.AllowEmpty && text.Length == 0) return new(false, "A value is required.");
        if (text.Length > request.MaximumLength) return new(false, $"Enter no more than {request.MaximumLength} characters.");
        if (request.InputType == TextInputType.Pin && (text.Length is < 4 or > 8 || text.Any(character => !char.IsAsciiDigit(character)))) return new(false, "PINs must contain four to eight digits.");
        if (request.InputType == TextInputType.Number && text.Any(character => !char.IsAsciiDigit(character))) return new(false, "Enter numbers only.");
        if (request.InputType == TextInputType.Url && (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) || !_allowedUrlSchemes.Contains(uri.Scheme))) return new(false, "Enter a complete HTTP or HTTPS URL.");
        if (request.InputType == TextInputType.FileName && text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return new(false, "The file name contains a character Windows does not allow.");
        if (request.InputType == TextInputType.PathDisplayOnly) return new(false, "Use the native file picker to choose a path.");
        foreach (TextInputValidationRule rule in request.ValidationRules)
        {
            if (rule.MinimumLength is { } minimum && text.Length < minimum) return new(false, rule.Message);
            if (rule.MaximumLength is { } maximum && text.Length > maximum) return new(false, rule.Message);
            if (!string.IsNullOrWhiteSpace(rule.Pattern) && !Regex.IsMatch(text, rule.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return new(false, rule.Message);
        }
        return new(true);
    }
}
