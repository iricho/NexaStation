# Private Alpha Tester Distribution

Phase 11.75 uses a signed x64 MSIX as the tester package.

Required identity:

- Package name: `NexaStation.Alpha`
- Publisher: `CN=NexaStation Alpha`
- Publisher display name: `NexaStation Alpha`
- Application ID: `App`
- Executable: `NexaStation.App.exe`
- Display version: `0.11.75-alpha.1`
- MSIX package version: `0.11.75.1`
- Channel: `Alpha`
- Architecture: `x64`

The release owner creates a local test certificate with `eng/New-AlphaTestCertificate.ps1`. The public `.cer` may be shipped to testers; the private `.pfx` and password must stay outside source control and CI logs.

Build flow:

1. Run `dotnet restore NexaStation.sln`.
2. Run `dotnet build NexaStation.sln -c Release -p:Platform=x64 --no-restore`.
3. Run `dotnet test NexaStation.sln -c Release -p:Platform=x64 --no-build --no-restore`.
4. Run `eng/Test-ReleaseReadiness.ps1`.
5. Build the signed MSIX with the Alpha certificate.
6. Verify the Authenticode signature and signer subject.
7. Build the tester ZIP with `eng/Build-PrivateAlphaBundle.ps1`.
8. Perform clean-machine install, launch, onboarding, update, uninstall, and diagnostics checks.
9. Run Windows App Certification Kit and record real pass/fail output.

The tester ZIP must not contain source code, `.pfx`, signing passwords, databases, logs, user data, personal artwork, API keys, debug symbols, bundled launchers, bundled emulators, ROMs, or commercial artwork.

Do not claim the package is tester-ready until clean-machine installation, update, uninstall, diagnostics export, Safe Mode, Recovery, single-instance activation, and WACK results are actually recorded.

The optional `.appinstaller` template is inactive for local ZIP distribution. Enable it only after HTTPS package and update URLs exist and update behavior has been tested without interrupting active games.
