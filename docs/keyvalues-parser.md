# Valve KeyValues parser

`IKeyValuesParser` is a generic, non-regex parser for quoted or bare keys/values and nested objects. It accepts whitespace, tabs, CR/LF, an optional UTF-8 BOM, empty objects, repeated keys, comments, escaped quotes, escaped backslashes, common control escapes, unknown fields, and arbitrary property order. Repeated keys remain separate child nodes.

Parsing is tolerant: the document contains all recoverable nodes plus diagnostics with file (added by the caller), one-based line, column, and a readable reason. Callers decide whether a malformed document is usable. Manifest/configuration contents are not logged. The Steam adapters interpret only their required fields and ignore future metadata safely.

Synthetic fixtures cover nested, repeated, escaped, BOM, modern library, legacy library, valid manifest, traversal, and malformed shapes. No real Steam manifest is checked in.
