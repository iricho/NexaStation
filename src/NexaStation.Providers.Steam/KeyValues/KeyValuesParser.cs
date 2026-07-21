using System.Text;

namespace NexaStation.Providers.Steam.KeyValues;

public sealed class KeyValuesParser : IKeyValuesParser
{
    public KeyValuesDocument Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Parser parser = new(content);
        return parser.Parse();
    }

    private sealed class Parser(string text)
    {
        private readonly List<KeyValuesDiagnostic> _diagnostics = [];
        private int _index;
        private int _line = 1;
        private int _column = 1;

        public KeyValuesDocument Parse()
        {
            KeyValuesNode root = new("$root");
            SkipTrivia();
            while (!End)
            {
                KeyValuesNode? node = ParseProperty();
                if (node is not null) root.Children.Add(node);
                else Recover();
                SkipTrivia();
            }
            return new(root, _diagnostics);
        }

        private KeyValuesNode? ParseProperty()
        {
            (string? key, int line, int column) = ReadToken();
            if (key is null) { Error(line, column, "Expected a key."); return null; }
            SkipTrivia();
            if (End) { Error(_line, _column, $"Key '{key}' is missing a value or object."); return new(key, string.Empty); }
            if (Peek == '{')
            {
                Advance();
                KeyValuesNode node = new(key);
                SkipTrivia();
                while (!End && Peek != '}')
                {
                    KeyValuesNode? child = ParseProperty();
                    if (child is not null) node.Children.Add(child); else Recover();
                    SkipTrivia();
                }
                if (End) Error(_line, _column, $"Object '{key}' is missing a closing brace."); else Advance();
                return node;
            }
            (string? value, int valueLine, int valueColumn) = ReadToken();
            if (value is null) { Error(valueLine, valueColumn, $"Key '{key}' is missing a value or object."); return new(key, string.Empty); }
            return new(key, value);
        }

        private (string? Value, int Line, int Column) ReadToken()
        {
            SkipTrivia(); int line = _line, column = _column;
            if (End || Peek is '{' or '}') return (null, line, column);
            if (Peek == '"')
            {
                Advance(); StringBuilder value = new();
                while (!End && Peek != '"')
                {
                    if (Peek == '\\')
                    {
                        Advance();
                        if (End) break;
                        char escaped = Peek;
                        if (escaped is '"' or '\\') value.Append(escaped);
                        else if (escaped is 'n' or 'r' or 't') value.Append(escaped switch { 'n' => '\n', 'r' => '\r', _ => '\t' });
                        else { value.Append('\\'); value.Append(escaped); }
                        Advance();
                    }
                    else { value.Append(Peek); Advance(); }
                }
                if (End) Error(line, column, "Quoted value is missing its closing quotation mark."); else Advance();
                return (value.ToString(), line, column);
            }
            StringBuilder bare = new();
            while (!End && !char.IsWhiteSpace(Peek) && Peek is not '{' and not '}') { bare.Append(Peek); Advance(); }
            return (bare.Length == 0 ? null : bare.ToString(), line, column);
        }

        private void SkipTrivia()
        {
            if (_index == 0 && !End && Peek == '\uFEFF') Advance();
            bool again;
            do
            {
                again = false;
                while (!End && char.IsWhiteSpace(Peek)) Advance();
                if (!End && Peek == '/' && LookAhead(1) == '/')
                {
                    while (!End && Peek != '\n') Advance();
                    again = true;
                }
            } while (again);
        }

        private void Recover()
        {
            while (!End && Peek != '\n' && Peek != '}') Advance();
            if (!End && Peek == '}') Advance();
        }

        private void Error(int line, int column, string reason) => _diagnostics.Add(new(null, line, column, reason));
        private char LookAhead(int offset) => _index + offset < text.Length ? text[_index + offset] : '\0';
        private char Peek => text[_index];
        private bool End => _index >= text.Length;
        private void Advance(){if(End)return;if(text[_index++]=='\n'){_line++;_column=1;}else _column++;}
    }
}
