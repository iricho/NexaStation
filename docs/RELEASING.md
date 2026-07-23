# Building a Windows release

NexaStation uses Velopack to create its Windows installer, portable bundle, update package, and release metadata from one self-contained `win-x64` publish.

## Prerequisites

- Windows x64
- .NET 10 SDK
- Visual Studio WinUI tooling for local development
- Git

The Velopack CLI is repository-local and pinned in `.config/dotnet-tools.json`; Inno Setup, a global packaging tool, and a signing certificate are not needed for a local build. Finished packages include .NET and Windows App SDK dependencies.

## Build command

From the repository root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Build-Release.ps1 -Version 0.1.0
```

The script restores the pinned tool and dependencies, runs the complete solution test suite, publishes once, invokes `vpk pack`, checks the expected output, and writes SHA-256 hashes. Use `-SkipTests` only when the same commit has already passed the complete suite.

## Output

```text
artifacts/releases/0.1.0/
|-- Iricho.NexaStation-Setup.exe
|-- Iricho.NexaStation-Portable.zip
|-- Iricho.NexaStation-0.1.0-full.nupkg
|-- releases.win.json
|-- assets.win.json
|-- SHA256SUMS.txt
`-- other Velopack release files
```

Velopack's channel-named setup and portable files are retained because its asset metadata references them. The two names above are byte-for-byte aliases intended for GitHub Release downloads.

The shared publish output remains at `artifacts/publish/win-x64`. Generated artifacts are ignored by Git.

## Storage behavior

Velopack tells NexaStation whether it is running as a portable application. Portable builds keep all mutable state under `<Velopack RootAppDir>\Data`, outside Velopack's versioned application-content directory. Move the whole extracted folder to retain its library and preferences.

Installed and development builds keep mutable user data under `%LocalAppData%\NexaStation`, separate from application binaries. A `portable.flag` is supported only as an optional fallback for unusual development runs where Velopack has not initialized its locator; package metadata always takes priority.

NexaStation does not check for or download updates automatically. The Velopack startup hook is present so install, update, and uninstall lifecycle commands can complete before the WinUI interface is created.

## Installer behavior

The setup is a per-user installer and normally needs no administrator elevation. It installs application files under `%LocalAppData%\Iricho.NexaStation`, registers NexaStation in Windows Installed Apps, and manages its Desktop and Start Menu shortcuts. Library data and preferences remain separately under `%LocalAppData%\NexaStation`, including after an application uninstall.

Do not extract the portable bundle into Program Files or another read-only location. Move the entire portable folder—not only an executable—so its `Data` folder and Velopack files stay together.

## Checking the release

```powershell
Get-Content .\artifacts\releases\0.1.0\SHA256SUMS.txt
Get-FileHash .\artifacts\releases\0.1.0\Iricho.NexaStation-Setup.exe -Algorithm SHA256
Get-FileHash .\artifacts\releases\0.1.0\Iricho.NexaStation-Portable.zip -Algorithm SHA256
```

Before publishing, extract and move the portable bundle, then test install, upgrade, launch, shortcuts, and uninstall with a disposable Windows account. Confirm portable data follows its extracted root and installed data remains under LocalAppData after uninstall.

## Unsigned builds

Release artifacts are not code-signed yet. Windows SmartScreen can show an unknown-publisher warning. The build does not create or install a fake or self-signed certificate.

No branded `.ico` asset is currently available, so the installer uses Velopack's default icon and the build prints a warning. Add a valid project `.ico` later to brand both the setup and updater.

## Versioning

Use a three-part Semantic Version such as `0.1.0`, `0.1.0-alpha.1`, or `0.1.0-beta.2`. Four-part Windows versions such as `0.1.0.0` are rejected by the release command.
