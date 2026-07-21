# Architecture

## Phase 12 activity and ecosystem boundaries

Domain/Core define provider-neutral activity, capture, remote-play, presence, achievement, and capability records/contracts. Infrastructure owns parameterized SQLite stores, capture validation/encoding, one optional Windows hotkey, approved-folder recording observation, remote argument validation/launching, and cached capability reporting. App only issues capture authorization after a direct UI/input action and presents settings/diagnostics. Launch coordination emits correlated activity and presence events; it never invokes capture. Gallery accepts a targeted file only when it belongs to an already approved enabled source.

NexaStation keeps Domain/Core/Infrastructure/App dependency layers plus isolated provider and desktop-control adapters. **Domain** contains library, provider, target, asset, query, and session models without SQLite or WinUI. **Core** contains provider/repository contracts, typed settings/state services, target validation, input/window/process abstractions, import orchestration, and the launch coordinator. **Infrastructure** implements SQLite, persistence, Windows process/shell launch, shortcut resolution, logs, gamepad polling, and AppWindow behavior. **Providers.Steam**, **Providers.Emulation**, and **Providers.WindowsApps** reference Domain/Core and own their external formats and discovery rules. **DesktopControl** owns bounded input translation and safety. **App** composes services and owns WinUI dialogs, dashboard projections, focus, and native pickers.

The app initializes the database before loading cards, repairs incomplete sessions, restores the current section and selected item, then asynchronously projects repository items into Home and section rows. UI changes update visible collections without restarting the app. Search and application-state writes are debounced.

The launch boundary remains `ILaunchCoordinator`: validate target → dispatch → optionally detect provider application → create persisted session → hide if configured → wait for tracked processes → complete once → restore foreground/fullscreen → restore card focus. The generic coordinator knows neither Steam formats nor SQL/WinUI. `ILaunchedApplicationDetector` supplies provider-aware process handoff.

Provider flow is detect → scan normalized records → preview/match → transactional import or refresh → ordinary library cards. Providers never write the database; `IProviderPersistence` maintains sources, unique provider identity, overrides, scan history, and multiple targets. Unavailable library sources remain state, preventing destructive refresh.

External-file safety is a core invariant. Repositories store paths and delete only database rows. Original programs, shortcuts, installations, and artwork are never moved, edited, or deleted.

# Phase 5 Gallery boundaries

`NexaStation.Providers.Gallery` owns approved-root traversal, media inspection, thumbnail/cache policy, refresh orchestration, and optional watchers. `NexaStation.Gallery` owns query navigation, viewport state, slideshows, albums, backgrounds, and safe external media launching. Domain/Core contain typed records and contracts; Infrastructure implements SQLite; App owns WinUI, native folder approval, focus, and asynchronous image presentation.

Gallery indexing is deliberately separate from `ILibraryProvider`: media files are not launchable game/application cards and have different privacy, availability, query, and cache semantics. `IGalleryPersistence` is the write boundary. Successful scans transact upserts and missing-state updates together, while failed/unavailable scans preserve the last good index. Lazy thumbnail generation uses bounded memory/disk LRUs and never writes beside source media.

# Phase 6 system and media boundaries

`NexaStation.SystemIntegration` isolates documented Windows Core Audio, media-session, controller, network, time, window, and power APIs. `NexaStation.Media` composes existing library/Gallery data, recent openings, and preferred-player policy. `NexaStation.QuickMenu` owns cached overlay state, action serialization, local notifications, confirmation, and hold cancellation. None of these projects scans providers or owns WinUI controls.

The app composes platform implementations and keeps the Quick Menu foreground-only. SQLite implementations remain in Infrastructure. System failures are represented as result/snapshot data so optional Windows capabilities cannot take down the dashboard.

# Phase 7 customization and presentation boundaries

`NexaStation.Customization` owns layout registration, dashboard configuration, pins, collections/rules, themes, sounds, performance presets, and validated configuration transfer. It depends on Domain/Core contracts and has no WinUI controls. `NexaStation.Presentation` converts those models into active-layout, card, and semantic-focus presentation state; it contains no provider discovery or SQL. Infrastructure implements `ICustomizationStore` with migrations 025–032. App owns the three WinUI trees, dialogs, theme resources, and the original local cue player.

All layouts project the same repositories and only one layout is the input owner. Semantic focus crosses layout/tree replacement by stable section, row, and target identity. Smart queries, row results, nesting, and caches are bounded. Import/export cannot address provider/library tables, while recovery creates a backup before clearing interface-only state.
# Phase 3 emulation boundary

`NexaStation.Providers.Emulation` contains adapters, capability probes, platform definitions, filename identification, local artwork discovery, safe scanning, template rendering, and launch resolution. It has no WinUI controls and no direct database writes.

Domain records live in `NexaStation.Domain`; contracts in `NexaStation.Core`; SQLite implementations in `NexaStation.Infrastructure`; controller-friendly setup/import dialogs remain in `NexaStation.App`. `LaunchCoordinator` resolves `LaunchType.Emulator` immediately before validation and process start, preserving the Phase 0 session lifecycle.
# Phase 4 Windows application and desktop-control boundaries

`NexaStation.Providers.WindowsApps` owns discovery, classification, icon caching, validation, and Windows activation. It depends only on Core/Domain contracts. `NexaStation.DesktopControl` owns gamepad sampling, cursor math, approved input injection, integrity checks, physical-mouse pause, foreground tracking, and session cleanup. Infrastructure owns migrations 012–015 and persistence. The WinUI app composes these layers and presents preview/settings/details.

The existing `ILibraryProvider`, `LaunchTarget`, `LaunchCoordinator`, application-state, artwork, and library repository seams are reused. Windows-specific identities remain structured fields rather than encoded command strings. Launch coordination separates activation success from tracking confidence; no reliable process means no invented duration. Desktop control begins only after tracking establishes a process and stops before frontend restoration.

# Phase 9 compatibility and quality boundaries

`NexaStation.Compatibility` owns typed profiles, safe rules, stable-identity matching, precedence/conflict reporting, validation, and the signed-feed foundation. It depends on Domain but not providers, SQL, WinUI, or process APIs. `LaunchCoordinator` consumes a validated resolution before normal target validation; providers never parse or execute profile JSON.

`NexaStation.Quality` owns normalized crash fingerprints, bounded semantic navigation events, deterministic focus scenarios, accessibility snapshots, synthetic datasets, benchmark/regression evaluation, and quality repository contracts. `NexaStation.Feedback` owns triage transitions, local reproduction ZIPs, redaction-aware issue templates, and explicit user handoff to a browser. Infrastructure implements migrations 033–037 and SQLite stores; App composes opt-in UI. No Phase 9 layer uploads telemetry or modifies external files.
# Phase 11 metadata and launcher boundaries

Domain/Core define provider-neutral identities, candidates, provenance, assignments, jobs, scoring, and contracts. Infrastructure implements structured remote/local providers, validation, protected credentials, durable orchestration, SQLite stores, and read-only launcher adapters. Emulation owns bounded disc-serial identification. App owns consent, account, bulk-job, and single-item entry points. External JSON and image bytes cross validation boundaries before persistence or presentation.

# Phase 11.5 item-action boundary

Core owns provider-neutral item context, descriptor, aggregation, validation, and execution-authorization contracts. Providers describe actions without side effects. App owns the reusable WinUI menu, confirmation, safe navigation, and dispatch to existing repositories/services. Persistence remains database-only for library, Gallery-index, and collection removals. See [item action architecture](item-actions/architecture.md).

# Phase 10 boundaries

# Phase 13 boundaries

`NexaStation.Interaction` owns focus-derived action targets and primary routing. `NexaStation.MediaPlayer` owns playback orchestration and its Windows engine adapter. `NexaStation.EmulatorDiscovery` owns bounded detection and validation. WinUI presents snapshots and confirmation only; SQLite persistence advances through immutable migration 074.

`NexaStation.Input`, `NexaStation.TextInput`, `NexaStation.Profiles`, and `NexaStation.Display` contain platform-neutral policy and contracts. Windows Gaming Input polling remains centralized in Infrastructure; WinUI owns only presenters/window movement. SQLite implements all Phase 10 stores through immutable migrations 038–044.
