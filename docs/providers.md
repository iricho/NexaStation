# Library providers

Providers discover normalized, local library records without writing the NexaStation database or controlling the UI. `ILibraryProvider` owns detection, scanning, validation, and launch-target resolution. `IProviderRegistry` exposes registered providers. `IProviderImportService` owns preview matching and delegates transactional persistence through `IProviderPersistence`.

Provider identity is the pair `ProviderId` + `ProviderItemId`. A library item may have more than one source and launch target. Provider-managed targets are labeled, enabled independently, and preserved beside user-managed targets. Manual edits create `MetadataOverrides`; refresh changes only provider-owned fields without an active override. Removing a provider source is never inferred from a disconnected drive.

Scans return availability, library-source health, normalized items, warnings, errors, and cancellation state. Provider implementations must be read-only toward external libraries, tolerate partial failures, bound file concurrency, avoid credentials, and keep provider-specific formats out of Domain and WinUI.

The solution registers `steam`, `emulation`, `windows-start-menu`, `windows-apps-folder`, `windows-pwa`, `epic`, `gog`, `ubisoft`, `ea`, and `battlenet`. Emulation uses configured installations and explicit game sources rather than global machine discovery. Storefront providers read only bounded local manifests or standard uninstall records and expose separate alternate launch targets. Xbox/Microsoft Store games use the packaged-app path with conservative Games-category evidence.

Provider availability varies by machine and launcher version. A missing safe stable identifier is reported as limited rather than guessed. No provider reads account credentials, starts a launcher during discovery, edits a manifest/configuration, or fabricates download progress. See the per-provider guides under `docs/providers`.

Emulated games use `IEmulationLibraryService` for source scanning/import and `IEmulatorLaunchResolver` for runtime command generation. The generic `ILibraryProvider` registration exposes availability and provider diagnostics, while emulator-specific rows remain in dedicated tables. This avoids forcing ROM paths, emulator profiles, and RetroArch cores into Steam-shaped provider data.
# Windows application providers

Phase 4 registers `windows-start-menu`, `windows-apps-folder`, and `windows-pwa`. The first scans Windows known-folder Programs roots and resolves `.lnk`/`.url` entries read-only. The second enumerates current-user package application-list entries and retains AUMID/shell identities. The third derives only medium/high-confidence installed web apps from those Windows-visible entries; it never scans browser profiles.

All three use the shared provider contract while a Windows-specific persistence service retains richer source data, categories, tracking, and controller assignments. Refresh never deletes library items. See [windows-app-provider.md](windows-app-provider.md).

# Gallery source provider

Gallery uses a dedicated `IGallerySourceScanner`/`IGalleryPersistence` contract because photos and videos are not ordinary launchable library items. Only explicitly approved roots are scanned; traversal is read-only, contained, and reparse-point aware. Metadata and thumbnail failures are item-local, bounded, and cancellable. Definitive successful scans can mark missing rows unavailable, while failed/disconnected scans cannot.

See [gallery-provider.md](gallery-provider.md), [gallery-scanning.md](gallery-scanning.md), and [gallery-privacy.md](gallery-privacy.md).

Provider item actions use structured persisted identities only. Manage installation opens an allowlisted provider URI or Windows Installed Apps and never executes uninstall metadata. Removing a provider item from NexaStation does not modify the provider and may be reversed by a later provider scan. See [provider management](item-actions/provider-management.md).

# Phase 7 presentation independence

Modern Dashboard, Classic Crossbar, and Compact Handheld consume the same provider-backed `LibraryItem` records. Providers never receive layout, theme, dashboard, pin, or collection concerns. Interface configuration exports contain stable references and presentation settings only; provider sources, credentials, scan results, and imported library records are never replaced by import or recovery.
