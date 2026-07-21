# Phase 9 verification evidence

Verified locally on 2026-07-19. This is engineering evidence for the current workspace, not a clean-machine or Beta certification.

## Passed

- Release solution build: 43 projects, 0 warnings, 0 errors.
- Automated suite: 635 passed, 0 failed, 0 skipped across 20 test assemblies.
- Database migration tests: supported Alpha schema 25, previous schema 32, current schema 37, empty/current databases, 5,000-item fixture, and Phase 9 record round trips.
- x64 package generation: unsigned `NexaStation.App_0.9.0.0_x64.msix`, 83,127,980 bytes. The host lacks `mspdbcmf.exe`, so the optional symbols package was not produced.
- Self-contained x64 startup smoke: process remained alive and responsive for 12 seconds, created a window, entered onboarding, then accepted a normal window-close request and recorded `CleanShutdown`. Observed working set was 139,120,640 bytes at the sample point. An earlier run also completed the 32-to-37 migration with a validated pre-migration backup.
- Dependency advisory audit: NuGet reported no known vulnerable direct or transitive packages for all 43 projects using the configured sources.
- Synthetic performance harness: completed 1,000/5,000 library, 10,000 Gallery, and 5,000 smart-collection scenarios. Exact quick-run medians are in `docs/performance/phase-9-results.md`.

## Investigated environmental failure

The first unpackaged, framework-dependent startup attempt exited before NexaStation's launch callback. Windows Event Log identified `REGDB_E_CLASSNOTREG` in Windows App SDK deployment initialization. Republishing with `PublishSelfContainedApp=true`, the mode used by pull-request CI, removed that host-runtime dependency and passed the startup smoke. This does not replace signed-MSIX installation testing.

## Not available in this environment

- Clean VM or clean standard-user Windows installation.
- Owner signing certificate, signed App Installer feed, published update endpoint, or package repair/update/uninstall laboratory.
- Repository remote or imported alpha issue/feedback dataset.
- Physical Xbox, PlayStation, Nintendo-layout, generic XInput/HID, wired/wireless, and multi-controller matrix.
- Representative legal Steam launcher/anti-cheat/multi-process titles, packaged app/PWA reuse cases, and all supported emulator versions.
- 720p/1080p/1440p plus 125%/150%/200% manual UI matrix.
- Narrator, High Contrast, Reduced Motion, controller-only Recovery, and controller-disconnect-in-dialog manual verification.
- Low-end hardware certification, CPU/GPU instrumentation, four-hour idle, two-hour navigation, 100-launch, and memory-leak soak runs.
- ARM64 hardware/package verification.

Because these release gates are missing, the current build remains Alpha and is not a Beta Candidate.
