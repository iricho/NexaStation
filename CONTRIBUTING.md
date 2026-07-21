# Contributing

NexaStation is not yet open for public distribution because its project license is undecided. Discuss contribution terms with the owner before submitting reusable work.

For authorized development:

1. Use Windows 10 1809 or later, the .NET 10 SDK, and Visual Studio with WinUI/MSIX tooling.
2. Run `dotnet restore NexaStation.sln`.
3. Run `dotnet build NexaStation.sln -c Release -p:Platform=x64`.
4. Run `dotnet test NexaStation.sln -c Release -p:Platform=x64 --no-build`.
5. Preserve local-first behavior, explicit opt-in, controller/keyboard/mouse access, and truthful diagnostics.
6. Add tests for migrations, recovery, provider boundaries, focus behavior, and release gates affected by a change.

Never commit user databases, logs, crash reports, diagnostics ZIPs, signing certificates, package passwords, or provider-specific local paths.

## Compatibility profiles

Use the contribution template and schema under `compatibility/`. Exact stable identity, reproduction steps, verification evidence, and safe declarative rules are mandatory. Display-name-only matching, executable behavior, personal paths, ROMs, copyrighted assets, secrets, controller serials, and unreviewed diagnostics are rejected. One maintainer review is required; high-impact/provider-wide rules require two.
