# Compatibility profiles

Compatibility profiles are declarative data, not plugins. Matching requires a stable provider item ID, executable identity, AUMID, platform/adapter pair, generalized controller family, display identity, or an explicitly provider-wide identity. Display names never activate rules.

Contributions must validate against `schema/compatibility-profile.v1.schema.json`, include reproduction and verification notes, contain no personal paths or copyrighted content, and receive maintainer review. High-impact provider-wide profiles require two reviews.

Forbidden content includes commands, scripts, PowerShell, Registry edits, file deletion/moving, process killing, elevation, security changes, executable downloads, ROMs, controller serial numbers, secrets, and arbitrary environment-variable extraction.
