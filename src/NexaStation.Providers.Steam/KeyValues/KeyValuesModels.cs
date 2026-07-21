namespace NexaStation.Providers.Steam.KeyValues;

public sealed record KeyValuesDiagnostic(string? File, int Line, int Column, string Reason);

public sealed class KeyValuesNode(string key, string? value = null)
{
    public string Key { get; } = key;
    public string? Value { get; } = value;
    public List<KeyValuesNode> Children { get; } = [];
    public bool IsObject => Value is null;
    public IEnumerable<KeyValuesNode> FindAll(string key) => Children.Where(child => string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase));
    public KeyValuesNode? Find(string key) => FindAll(key).FirstOrDefault();
    public string? ValueOf(string key) => Find(key)?.Value;
}

public sealed record KeyValuesDocument(KeyValuesNode Root, IReadOnlyList<KeyValuesDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Count > 0;
}

public interface IKeyValuesParser
{
    KeyValuesDocument Parse(string content);
}
