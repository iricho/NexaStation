namespace NexaStation.TextInput.Sessions;

public sealed class TextInputBuffer
{
    private readonly int _maximumLength;
    private readonly bool _sensitive;
    private readonly List<char> _characters;

    public TextInputBuffer(string? initialText, int maximumLength, bool sensitive = false)
    {
        if (maximumLength is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(maximumLength));
        _maximumLength = maximumLength;
        _sensitive = sensitive;
        _characters = (initialText ?? string.Empty).Take(maximumLength).ToList();
        Cursor = _characters.Count;
    }

    public int Cursor { get; private set; }
    public int Length => _characters.Count;
    public string Text => new(_characters.ToArray());
    public bool IsSensitive => _sensitive;

    public bool Insert(string value)
    {
        if (string.IsNullOrEmpty(value) || _characters.Count >= _maximumLength) return false;
        char[] accepted = value.Take(_maximumLength - _characters.Count).ToArray();
        _characters.InsertRange(Cursor, accepted);
        Cursor += accepted.Length;
        return accepted.Length > 0;
    }

    public bool Backspace()
    {
        if (Cursor <= 0) return false;
        _characters.RemoveAt(--Cursor);
        return true;
    }

    public bool Delete()
    {
        if (Cursor >= _characters.Count) return false;
        _characters.RemoveAt(Cursor);
        return true;
    }

    public void MoveCursor(int delta) => Cursor = Math.Clamp(Cursor + delta, 0, _characters.Count);
    public void Clear()
    {
        if (_sensitive) for (int index = 0; index < _characters.Count; index++) _characters[index] = '\0';
        _characters.Clear(); Cursor = 0;
    }
}
