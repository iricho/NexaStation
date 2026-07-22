# Building a Windows release

NexaStation ships as a portable ZIP and a normal per-user Windows installer. Both packages come from the same self-contained `win-x64` publish directory, so they contain the same application binaries and runtime.

## Prerequisites

- Windows x64
- .NET 10 SDK
- Visual Studio 2026 with the Windows App SDK/WinUI tools when working in Visual Studio
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) for the installer EXE

No .NET runtime, Visual Studio installation, or Windows App SDK installation is required on the computer that runs the finished release.

## Build command

From the repository root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Build-Release.ps1 -Version 0.1.0
```

The command restores dependencies, runs the solution tests, publishes NexaStation once, creates both packages, and writes SHA-256 checksums. Use `-SkipTests` only when the same commit has already passed the complete suite. Use `-PortableOnly` when Inno Setup is intentionally unavailable.

## Output

```text
artifacts/release/0.1.0/
├── NexaStation-Portable-0.1.0-win-x64.zip
├── NexaStation-Setup-0.1.0-win-x64.exe
└── SHA256SUMS.txt
```

The shared unpackaged publish output is kept at `artifacts/publish/win-x64`. Generated `artifacts` content is ignored by Git.

## Portable behavior

`portable.flag` beside `NexaStation.App.exe` activates portable mode. All mutable state—including the library database, settings, logs, caches, downloaded artwork, generated UI sounds, backups, and custom theme assets—stays under the extracted `Data` folder.

Move the complete extracted folder to preserve its library and preferences. Running from a read-only folder, a protected system directory, or read-only removable media is unsupported; NexaStation displays the data-folder error at startup.

## Installed behavior

The installer runs per user without administrator privileges and installs the application under `%LocalAppData%\Programs\NexaStation`. Mutable user data remains separate under `%LocalAppData%\NexaStation`.

Upgrades reuse the same installer identity and installation directory. Repair, upgrade, and normal uninstall do not delete the library database, settings, artwork, caches, backups, logs, or other personal NexaStation data.

## Checking the release

Verify the hashes with:

```powershell
Get-FileHash .\artifacts\release\0.1.0\NexaStation-Portable-0.1.0-win-x64.zip -Algorithm SHA256
Get-FileHash .\artifacts\release\0.1.0\NexaStation-Setup-0.1.0-win-x64.exe -Algorithm SHA256
Get-Content .\artifacts\release\0.1.0\SHA256SUMS.txt
```

Before publishing, test the portable ZIP after extracting and moving it, then test install, upgrade, launch, shortcuts, and uninstall with a disposable Windows user account. Confirm that `portable.flag` is absent from the installed directory and that uninstall leaves `%LocalAppData%\NexaStation` intact.

## Unsigned builds

These packages are not code-signed. Windows SmartScreen may show an unknown-publisher warning until a trusted signing process is added. The release script does not create or install a fake or self-signed certificate.
