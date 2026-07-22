# Installation

## Development

Install the .NET 10 SDK and Visual Studio WinUI/MSIX tooling, then restore, build, and test the x64 solution. Run the packaged profile for startup-task/protocol validation; unpackaged runs intentionally report that the startup task is unavailable.

## Portable ZIP and Windows installer

Normal Windows releases are available as a portable ZIP and a per-user installer. The installer places the application under `%LocalAppData%\Programs\NexaStation` while keeping personal data under `%LocalAppData%\NexaStation`. The portable package contains `portable.flag` and keeps all mutable data inside its extracted `Data` folder.

See [Building a Windows release](RELEASING.md) for prerequisites, commands, checksums, and verification steps.

## Alpha package

An alpha MSIX must be signed by the release owner and match the Publisher in the manifest/appinstaller. Install the trusted signing certificate only through an approved release channel, then install the `.msix` or `.appinstaller`. Do not bypass Windows trust warnings for an unverified artifact.

Start-with-Windows is off by default and can be requested during onboarding or changed later in **Startup, Recovery and Diagnostics**. Windows/Task Manager can disable it, and NexaStation will report that state rather than silently overriding it.

Uninstalling the package does not delete original games, apps, emulator files, or gallery media. Remove `%LocalAppData%\NexaStation` separately only if local library data, backups, and caches are no longer needed.
