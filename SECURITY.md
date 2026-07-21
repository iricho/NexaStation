# Security policy

## Supported builds

Only the latest development or explicitly published alpha build is supported. No public alpha is currently released.

## Reporting

Report suspected vulnerabilities privately to the project owner. Do not attach a NexaStation database, raw log, crash report, diagnostics archive, Steam library manifest, gallery index, signing certificate, or personal path to a public issue.

Use **Startup, Recovery and Diagnostics → Export redacted diagnostics** only when requested, inspect the ZIP before sharing, and remove anything you do not want to disclose. Diagnostics are local until the user shares them.

NexaStation does not elevate. Report any flow that executes an unexpected target, bypasses explicit confirmation, writes outside the managed data/cache/package locations, exposes secrets in logs, or restores an unvalidated archive.

## Capture and ecosystem safety

Automatic screenshot capture and automatic recording are forbidden. Each screenshot path requires a short-lived, current-session, action-bound, single-use gesture token. There are no launch/exit/timer/provider/controller/startup/background capture triggers. The optional global hotkey uses `RegisterHotKey`, is off by default, records no arbitrary keystrokes, and is disabled in Safe Mode. Capture bounds dimensions/output, validates image signatures, writes atomically, and creates no success record before the file is valid.

Remote-play arguments use a validated argument list without a shell; chaining, redirection, expansion, newlines, and unknown variables are rejected. No remote credentials are stored. Normal builds expose no mobile-control listener. Discord/achievement integrations do not scrape credentials or browser state, and unsupported progress is never fabricated.

Remote artwork must use HTTPS and pass redirect, MIME, signature, completion, byte-size, and dimension validation before an atomic move into managed storage. Provider JSON is treated as untrusted data and never executed. Artwork providers do not scrape HTML. Launcher discovery is read-only and must not access launcher credentials or rewrite manifests/configuration.

Compatibility data is untrusted until fully validated. Remote feeds remain disabled unless HTTPS, a pinned owner-approved key, payload digest, signature, schema, and every typed rule validate. Unknown/executable fields reject the whole update and preserve the previous known-good feed. Providers never execute community JSON.
Item actions are authorized from a current capability-filtered descriptor set. Descriptors cannot carry delegates, scripts, commands, or uninstall strings. Installation management opens only an allowlisted provider URI or Windows Installed Apps after confirmation; NexaStation does not elevate or run an uninstaller. Local-location actions require an existing filesystem path and open Explorer only.

## Local profile and input safety

## Phase 13 media and executable safety

Media and subtitle files are treated as untrusted data and are passed only to Windows media APIs; scriptable subtitle formats are rejected. External players require an existing executable, reject shell programs and command chaining, and launch without a shell. Emulator scans are bounded, skip reparse points, never execute candidates, and reject installers. Source media, ROMs, BIOS files, firmware, saves, and emulator configuration are never deleted or modified by these flows.

The emergency input reset (`Ctrl+Shift+I`, plus the reserved controller chord) cannot be removed by remapping. Conflicting or incomplete safety mappings are rejected before persistence. Owner PIN verification is rate-limited and uses constant-time hash comparison, but it is a local frontend boundary—not Windows authentication, encryption, or parental-control software. Local recovery can deliberately reset the PIN for a user who controls the PC.
# Built-in UI themes

Phase 13.5 supports built-in themes only. Theme manifests cannot declare commands, provider behavior, secrets, scripts, remote XAML, remote code, or arbitrary local/remote asset paths. Safe Mode ignores the stored UI theme and forces the built-in Nexa theme; invalid, unknown, or failed theme construction also falls back to Nexa. Downloaded third-party themes are outside the supported threat model.
