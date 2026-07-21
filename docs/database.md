# Database

## Phase 12 activity and ecosystem schema

- 055–056 add indexed activity events and bounded history/retention preferences.
- 057–058 add capture settings (global hotkey off by default) and validated captured-media metadata.
- 059–060 add user-managed remote-play profiles and correlated sessions.
- 061 adds disabled-by-default presence settings without Discord credentials.
- 062–063 add capability/provider state and verified achievement records; no provider is enabled by default.
- 064 adds a versioned cached ecosystem capability snapshot.

Activity metadata is capped at 16 KiB. `(SessionId, EventType)` prevents duplicate completion events. Captured paths are unique. Retention never deletes `PlaySessions` or external files.

The production database is `%LocalAppData%\NexaStation\Data\nexastation.db`; packaged builds can redirect LocalAppData beneath the package LocalCache. Tests always use unique temporary databases.

## Phase 11 metadata and launcher schema

- 045 registers metadata providers and safe capability/status data.
- 046–047 store external game identities and field-level provenance.
- 048 stores current artwork assignments plus bounded rollback history.
- 049–051 store durable jobs, per-item states, and review candidates.
- 052 stores Credential Manager references only—never secrets.
- 053–054 store launcher installation/discovery state.

Downloaded image bytes live in the managed filesystem, not SQLite. Job counters are updated only after terminal item state is committed. User-modified metadata and locked/user-selected artwork block automatic replacement.

## Schema and migrations

- `001_InitialLibrary`: `LibraryItems`, `LaunchTargets`, `Assets`, indexes and cascading foreign keys.
- `002_PlaySessions`: `PlaySessions` plus item/start indexes and non-negative duration constraint.
- `003_ApplicationState`: scalar `Settings` and `ApplicationState` tables.
- `004_ProviderArchitecture`: `Providers`, `ProviderLibrarySources`, `LibraryItemSources`, provider-aware launch/session columns, indexes, and the `steam` registration.
- `005_MetadataOverrides`: user ownership markers keyed by library item and field.
- `006_ProviderScanHistory`: compact scan outcome/count history without manifest bodies.
- `SchemaMigrations`: immutable version, name, and UTC application timestamp.

Embedded migrations are discovered by numeric prefix, applied in ascending order, and recorded inside the same transaction. Initialization is idempotent and refuses a database newer than the application. Every opened connection enables foreign keys and a five-second busy timeout; initialization enables WAL and `synchronous=NORMAL`.

Item add/update operations transactionally write the item, targets, and asset references. Provider batch import transactionally creates cards, provider sources, launch targets, and cached-art records; `(ProviderId, ProviderItemId)` is unique. Refresh matches that identity, updates unowned fields, and marks missing only after a successful scan of an available source. Deleting a library item cascades to targets, assets, sessions, sources, and overrides. No repository deletes a referenced external file or provider cache during ordinary row deletion.

Close NexaStation before copying the database. With WAL enabled, include `nexastation.db-wal` and `nexastation.db-shm` if they exist. A future release should create an automatic backup before applying new migrations; the current initializer deliberately never deletes or recreates a database after failure.
# Schema versions 7–11

- 007 adds user-managed emulator installations with normalized adapter/path uniqueness.
- 008 adds seeded platform definitions and explicit game folders without overwriting later user state.
- 009 adds emulated-game sources and indexes.
- 010 adds launch profiles, RetroArch cores, and structured emulator references on launch targets.
- 011 adds ordered multi-disc records. Automatic disc switching is intentionally absent.

Provider identity is `PlatformId + normalized canonical game path`, not filename alone. Foreign keys use cascade only for NexaStation-owned rows; external files are never deleted.
# Phase 4 schema

- Migration 012 adds `WindowsApplicationSources`, Windows launch identity columns, indexes, and three provider seeds.
- Migration 013 adds `ApplicationCategories`, item-category links, a unique primary-category rule, seeded built-ins, and `ApplicationCardStyle`.
- Migration 014 adds desktop-control profiles/assignments and Basic Mouse, Media Application, and Browser templates.
- Migration 015 adds application tracking policy plus application session fields to launch targets and play sessions.

Provider/source identities are unique. Imports use one transaction with per-item savepoints so a bad entry does not corrupt successful siblings. Complete successful refreshes update provider-owned fields and mark disappeared sources unavailable; cancelled/failed scans never mark missing. Foreign-key cascades remove NexaStation records only. User categories and manual targets remain authoritative.

# Phase 5 Gallery schema

- 016 adds approved `GallerySources` and source/privacy/scan settings.
- 017 adds unique source/path `GalleryMediaItems`, filesystem/display metadata, availability, favorites, hidden state, and preferred-player override.
- 018 adds rebuildable `GalleryThumbnails` with cache fingerprint, dimensions, size, and LRU timestamps.
- 019 adds `GalleryAlbums`, ordered memberships, smart/folder album fields, and cover references.
- 020 adds singleton `GalleryApplicationState`, scoped `GalleryLaunchPreferences`, and managed Gallery background selection.

SQLite stores paths and lightweight metadata, not image/video blobs. A complete refresh upserts and marks missing within one transaction; a failed or unavailable scan retains the last good rows. Deleting a source cascades only through Gallery-owned rows. Cache-file cleanup is explicit and constrained to the NexaStation Gallery cache root.

# Phase 6 media and system-action schema

- 021 adds scoped `MediaAssociations` referencing library applications without owning them.
- 022 adds success-only `RecentMedia`; deleted Gallery/library references become null so history can remain unavailable rather than disappear.
- 023 adds non-sensitive Quick Menu key/value preferences.
- 024 adds minimal system-action result history: action type, timestamps, result, and failure text.

No credentials, track-listening history, playback position, controller serials, IP addresses, or Wi-Fi secrets are stored. All new persistence uses parameters and UTC timestamps.

# Phase 7 interface customization schema

- 025 adds per-layout `DashboardConfigurations`.
- 026 adds ordered, bounded `DashboardRows` with typed sources and card presentation.
- 027 adds typed, ordered `Pins` by location.
- 028 adds nested `Collections` and ordered mixed-target `CollectionItems`.
- 029 adds structured `SmartCollectionRules`.
- 030 adds custom `Themes` and local background metadata.
- 031 adds singleton `UiSoundSettings`.
- 032 adds versioned `InterfaceConfigurationExports` used for recovery/import backups.

Customization foreign keys cascade only through NexaStation-owned interface records. Import applies interface tables in one transaction. Library, provider, Gallery, and external-file records are not part of the snapshot.
