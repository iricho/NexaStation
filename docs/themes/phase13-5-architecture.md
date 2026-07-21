# Phase 13.5 theme architecture

The new theme boundary consists of three projects:

- `NexaStation.UI.Shared`: navigation destinations, settings descriptors/search, theme contracts, registry, manager, and state interfaces.
- `NexaStation.UI.Themes.Nexa`: default Nexa manifest, resources, shell/settings presentation, and Nexa-specific destination types.
- `NexaStation.UI.Themes.Crossbar`: original Crossbar manifest, resources, horizontal shell, vertical secondary navigation, settings presentation, and Crossbar-specific destination types.

The two theme projects reference Shared and never reference each other. Domain, provider, storage, launch, media, Gallery, input, recovery, and other feature projects do not reference theme controls.

## Contracts

`INexaTheme` exposes immutable metadata, shell/settings factories, destination mappings, resource bundles, and validation. Resource bundles expose factories so manifest validation remains headless and WinUI dictionaries activate only for a running shell. `IThemePageRegistry` resolves a destination for a theme ID. `IThemeManager` owns initialize, preview, apply, cancel-preview, safe restore, persistence, validation, and disposal of the inactive shell.

The required destination set is represented by `NavigationDestination`: Home, Games, Apps, Media, Gallery, Library, Collections, Settings, Search, Details, ContextMenu, QuickMenu, Onboarding, Recovery, SafeMode, MediaPlayer, ArtworkSearch, MatchReview, EmulatorDiscovery, ActivityTimeline, Updates, About, Privacy, DiagnosticsExport, and ExitToDesktop.

Both manifests map every destination to a type in their own namespace. Contract tests compare the sets and prevent type aliasing between themes.

## Switching and recovery

Preview captures the current stable navigation state and does not persist. Cancel recreates the preview origin. Apply validates, creates the new shell, restores captured navigation state, persists the selected and last-known-healthy IDs, disposes the inactive shell, and raises one theme-change event. Unknown IDs, invalid manifests, and shell-construction failures fall back to `nexa.default`. Safe Mode ignores persisted selection and forces Nexa.

Theme settings use generic settings storage (`UI.ActiveThemeId`, `UI.LastHealthyThemeId`, and `UI.ThemeVersion`); no database migration is required.

## Crossbar design and provenance

Crossbar uses an original blue/teal gradient, a locally drawn abstract wave, Segoe system glyphs, horizontal major categories, vertical secondary categories, a content surface, and text controller prompts. It contains no Sony marks, PlayStation UI captures, official wallpaper, official console sounds, copied fonts, remote XAML, scripts, remote code, or arbitrary asset paths. The PS5 button PNG files supplied with Phase 13 are not copied into this theme.

Reduced Motion makes the decorative wave static and lower contrast. High Contrast support is declared and must still be visually verified against the running WinUI app. Only bundled code-defined resources are accepted in this phase; downloaded third-party themes are unsupported.

## Architectural limitation

The manager, manifests, independent settings trees, preview shell, persistence, validation, and fallback are implemented. Applying Crossbar now activates the persisted functional `ClassicCrossbar` layout for the live Home, Games, Apps, and Library dashboard and applies Crossbar chrome/resources to the shared Gallery, Media, and Collections surfaces. The production `MainWindow` feature composition remains monolithic, however, and the mapped destination controls are still contract markers rather than independently feature-bound pages. Navigation-state capture is not yet wired to every live dashboard focus/scroll detail.

Consequently, this architecture is a compile- and contract-valid foundation, not completed full-shell functional parity. It must not be described as a finished theme switch or a tester-ready Phase 13.5 build until the live root composition and feature bindings are migrated.
