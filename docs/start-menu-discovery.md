# Start Menu discovery

NexaStation asks Windows for the current-user and common Programs known folders instead of assuming localized folder names. It recursively reads only `.lnk` and `.url` entries inside those roots, with bounded concurrency, cancellation, progress, and per-entry diagnostics.

Shell Links are loaded read-only through the Windows Shell Link COM interface. Target, raw arguments, tokenized argument boundaries, working directory, description, icon location, run state, and relative path are retained where Windows exposes them. Internet shortcuts are parsed read-only and become URI candidates. The original shortcut remains the launch identity whenever possible; NexaStation never rewrites or resolves it destructively.

Folder names are weak category hints, not authority. Documentation, maintenance tools, and uninstallers are classified separately and are not recommended by default. Unreadable links do not abort the batch.
