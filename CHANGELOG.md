# Changelog

## Appearance and scrolling overhaul - Unreleased

- Refresh Nexa with midnight surfaces, teal accents, clearer typography, navigation states, card borders, and consistent toolbar styling.
- Give Library a bounded, independently scrolling results grid with stationary search and filters; restore mouse-wheel and keyboard scrolling after visiting Home.
- Size cards evenly across available columns and use the actual column count for directional focus navigation.
- Keep artwork inside card bounds so portrait covers cannot clip Home captions; reapply Home sizing after dashboard refreshes.
- Update existing shared brushes during theme changes and provide readable light, dark, and high-contrast surface palettes.
- Add layout and contrast regression coverage; see `docs/appearance-overhaul.md` for verification details.

## Library and reliability overhaul - Unreleased

- Keep library search active across sort, filter, favorite, and refresh actions without changing Home content.
- Add library result counts, clear filters, loading feedback, empty states, and an inline retry action.
- Load libraries beyond the first 1,000 items using stable paging; batch related database reads in groups of 200.
- Serialize dashboard and Gallery refreshes; run debounced Gallery search on the UI context and publish its rows only after a successful load.
- Apply hidden-item and guest restrictions to pinned and Dynamic Shelf rows on Home.
- Recalculate card sizing when results change and allow a single column in narrow layouts; wrap empty-state text within available space.
- Accept newer stable .NET 10 SDK feature bands for development.
- Add regression coverage for search state, filtering, deterministic ordering, paging, cancellation, and batch hydration.

## Phase 13.5 (foundation)

- Replaced the flat Settings flyout entries with a shared 20-category descriptor catalog, deterministic search, breadcrumbs, deep-link focus, section reset metadata, and separated advanced/recovery areas.
- Added isolated Shared, Nexa, and original Crossbar UI projects with stable theme manifests, all required destination mappings, preview/apply/cancel persistence, Safe Mode enforcement, and fallback to Nexa.
- Added distinct Nexa and Crossbar Settings visual trees plus an original code-drawn Crossbar shell preview and controller navigation structure.
- Added 54 settings, theme-contract, manifest-validation, fallback, and parity tests.
- Full live feature-tree replacement, runtime parity, packaging launch, hardware, accessibility, resolution, latency, CPU, and memory verification remain incomplete; Phase 13.5 is not yet tester-ready.

## 0.12.0 - Unreleased

- Added migrations 055–064 for local activity, capture settings/media, remote-play profiles/sessions, optional presence, achievement provider state, and cached ecosystem capabilities.
- Added privacy-filtered activity queries, bounded retention, deletion, and explicit redacted JSON/CSV export tied to existing launch sessions.
- Added direct-action-only PNG/JPEG screenshot capture, atomic validation/storage, approved-source Gallery indexing, opt-in conflict-aware hotkey, and visible Snipping Tool fallback.
- Added safe remote-play profiles/launch restoration, external recording-folder observation, optional presence abstractions, and honest achievement/download/mobile capability states.
- Explicitly prohibited all automatic capture and automatic recording triggers.

## 0.11.0 - Unreleased

- Added privacy-gated local, SteamGridDB, and TheGamesDB artwork providers; ScreenScraper is explicitly disabled pending an approved developer-credential strategy.
- Added deterministic game identities/scoring, bounded PS2 serial extraction, metadata/artwork provenance, protected Windows credentials, validated content-addressed downloads, assignment history, and rollback.
- Added persistent bounded artwork jobs, single-game and bulk fill-missing actions, durable review candidates, restart recovery, and protection for user-selected/locked assets.
- Added read-only Epic, GOG, Ubisoft Connect, EA app, and Battle.net discovery plus conservative Xbox/Store game classification and duplicate linking as alternate targets.
- Added immutable migrations 045–054 and Phase 11 security, privacy, provider, identity, and troubleshooting documentation.

## 0.10.0 - Unreleased

- Added semantic input actions, stable active-device detection, controller-family recognition, adaptive text glyphs, per-family navigation preferences, conflict-checked remapping, and an emergency mapping reset.
- Added a controller/touch-friendly local on-screen keyboard with QWERTY, AZERTY, Arabic, Hebrew, numeric, and symbol layouts; RTL presentation; validation; sensitive PIN entry; and privacy-bounded local suggestions.
- Added local Owner/Guest profiles, optional salted PBKDF2 owner PINs, page and per-library-item Guest restrictions, and temporary Guest activity cleanup.
- Added multi-monitor preference/fallback handling, TV safe-area and interface-scale calibration, HDR guidance, battery presentation, and battery-aware visual policy.
- Added immutable migrations 038–044, focused input/text/profile/display tests, adaptive onboarding/recovery prompts, and Phase 10 Beta UX documentation.

The development MSIX remains unsigned and therefore is not an installable public Beta artifact. Signing, clean-machine packaging validation, and representative hardware/manual couch testing remain release gates.

## 0.9.0 - Unreleased

- Added safe declarative compatibility profiles with stable-identity matching, source precedence, version constraints, conflict diagnostics, and local user overrides.
- Added compatibility reports, crash fingerprints, performance baselines, regression cases, and immutable migrations 033–037.
- Added a disabled-by-default signed compatibility-feed foundation that requires HTTPS, a trusted key, digest/signature verification, and complete schema/rule validation.
- Added semantic navigation auditing, accessibility test helpers, privacy-safe reproduction bundles, and user-driven GitHub issue preparation without automatic upload or submission.
- Added deterministic synthetic benchmark and migration-compatibility harnesses plus public compatibility/contribution and beta-readiness documentation.

NexaStation is not yet a Beta Candidate. Signed-package update testing, representative hardware/provider coverage, long-running soak tests, low-end measurements, manual accessibility verification, licensing, and clean-machine validation remain release blockers.

## 0.8.0 - Unreleased

- Added resumable ten-step onboarding with explicit startup/privacy choices.
- Added centralized paths, startup health, crash-loop detection, safe mode, and a controller-friendly recovery surface.
- Added database integrity checks, migration-history validation, pre-migration online backups, SHA-256 backup manifests, validated restore, and reset preservation.
- Added bounded/redacted logs, local crash reports, and on-demand diagnostics export.
- Added tested single-instance activation routing and `nexastation://section/...` routes.
- Added opt-in packaged Windows startup task, centralized release metadata, release-readiness checks, and update policy foundations.
- Added public documentation and CI/release workflow foundations.

Public alpha remains blocked until an owner-approved project license, signing identity, distribution URLs, and clean-machine validation are complete.

## Phase 13

- Added the Windows-backed NexaStation native local-video player, local resume/progress, track controls, fullscreen, queues, and safe external-player recovery.
- Added conservative known-emulator discovery and confirmation, opt-in portable folders, and migration-backed provenance models.
- Fixed focused-card actions through stable target routing across games, apps, Gallery, collections, and Discover.
- Proposed Pictures during fresh onboarding with opt-out, privacy, Home visibility, and subfolder controls.
- Added semantic Cross, Circle, Square, Triangle, and D-pad assets with accessibility and High Contrast fallbacks.
