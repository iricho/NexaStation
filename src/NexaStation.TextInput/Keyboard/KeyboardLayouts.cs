namespace NexaStation.TextInput.Keyboard;

public enum KeyboardTextDirection { LeftToRight, RightToLeft }
public enum KeyboardKeyKind { Character, Backspace, Shift, Space, Layout, Symbols, Clear, Submit, Cancel, CursorLeft, CursorRight }
public sealed record KeyboardKey(string Value, string Label, KeyboardKeyKind Kind = KeyboardKeyKind.Character, double Width = 1, string? ShiftValue = null, string? AccessibilityLabel = null);
public sealed record KeyboardRow(IReadOnlyList<KeyboardKey> Keys);
public sealed record KeyboardLayout(
    string Id,
    string DisplayName,
    string LanguageTag,
    KeyboardTextDirection Direction,
    IReadOnlyList<KeyboardRow> Rows,
    IReadOnlyList<KeyboardRow> ShiftLayer,
    IReadOnlyList<KeyboardRow> SymbolLayer,
    IReadOnlyList<KeyboardRow> NumericLayer,
    bool SupportsSuggestions,
    bool BuiltIn);

public interface IKeyboardLayoutCatalog
{
    IReadOnlyList<KeyboardLayout> Layouts { get; }
    KeyboardLayout GetById(string id);
    KeyboardLayout GetDefault(Sessions.TextInputType inputType);
}

public sealed class BuiltInKeyboardLayoutCatalog : IKeyboardLayoutCatalog
{
    public BuiltInKeyboardLayoutCatalog() => Layouts = [Qwerty(), Azerty(), Arabic(), Hebrew(), Numeric(), Symbols()];
    public IReadOnlyList<KeyboardLayout> Layouts { get; }
    public KeyboardLayout GetById(string id) => Layouts.FirstOrDefault(layout => string.Equals(layout.Id, id, StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException($"Unknown keyboard layout '{id}'.");
    public KeyboardLayout GetDefault(Sessions.TextInputType inputType) => inputType is Sessions.TextInputType.Number or Sessions.TextInputType.Pin ? GetById("numeric") : GetById("en-qwerty");

    private static KeyboardLayout Qwerty() => Latin("en-qwerty", "English QWERTY", "en", ["qwertyuiop", "asdfghjkl", "zxcvbnm"]);
    private static KeyboardLayout Azerty() => Latin("en-azerty", "English AZERTY", "en", ["azertyuiop", "qsdfghjklm", "wxcvbn"]);
    private static KeyboardLayout Arabic() => Script("ar", "Arabic", "ar", KeyboardTextDirection.RightToLeft, ["ضصثقفغعهخحجد", "شسيبلاتنمكط", "ئءؤرلاىةوزظ"]);
    private static KeyboardLayout Hebrew() => Script("he", "Hebrew", "he", KeyboardTextDirection.RightToLeft, ["קראטוןםפ", "שדגכעיחלךף", "זסבהנמצתץ"]);
    private static KeyboardLayout Numeric() => new("numeric", "Numeric", string.Empty, KeyboardTextDirection.LeftToRight, Rows(["123", "456", "789", "0"]), [], [], [], false, true);
    private static KeyboardLayout Symbols() => new("symbols", "Symbols", string.Empty, KeyboardTextDirection.LeftToRight, Rows(["!@#$%^&*()", "-_=+[]{}", "'\";:,.?/\\"]), [], [], [], false, true);

    private static KeyboardLayout Latin(string id, string name, string languageTag, string[] rows)
    {
        IReadOnlyList<KeyboardRow> normal = Rows(rows);
        IReadOnlyList<KeyboardRow> shifted = Rows(rows.Select(row => row.ToUpperInvariant()).ToArray());
        return new(id, name, languageTag, KeyboardTextDirection.LeftToRight, normal, shifted, Symbols().Rows, Numeric().Rows, true, true);
    }

    private static KeyboardLayout Script(string id, string name, string languageTag, KeyboardTextDirection direction, string[] rows) => new(id, name, languageTag, direction, Rows(rows), Rows(rows), Symbols().Rows, Numeric().Rows, true, true);
    private static KeyboardRow[] Rows(string[] rows) => rows.Select(row => new KeyboardRow(row.EnumerateRunes().Select(rune => new KeyboardKey(rune.ToString(), rune.ToString(), AccessibilityLabel: rune.ToString())).ToArray())).ToArray();
}
