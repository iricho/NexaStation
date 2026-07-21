# Categorized settings architecture

Phase 13.5 introduces one declarative settings catalog in `NexaStation.UI.Shared`. It is presentation-neutral and is consumed by both built-in themes. The active theme owns the settings visual tree; the catalog owns identity, grouping, semantics, defaults, validation metadata, and navigation targets.

## Information architecture

The catalog has 20 ordered categories:

1. Personalization
2. Input and Controllers
3. Games and Providers
4. Emulators
5. Artwork and Metadata
6. Applications
7. Media Player
8. Gallery
9. Capture
10. Audio and Media Sessions
11. Display and Television
12. Network and Remote Play
13. Activity and Privacy
14. Startup and Windows
15. Updates
16. Storage and Data
17. Accessibility
18. Help and About
19. Advanced
20. Reset and Recovery

Advanced settings are marked and placed near the end. Disruptive actions are marked dangerous and are valid only in Reset and Recovery. Development-only descriptors compile only in Debug builds.

## Descriptors

`SettingDescriptor` gives every setting a stable ID, category, group, title, description, keywords, control type, value binding, default, validation rule, restart/experimental/dangerous/advanced flags, navigation destination, sort order, save behavior, and recommendation state.

`SettingsCatalog.Validate` rejects duplicate IDs, unknown categories, missing user-facing metadata, controls without a value binding, and dangerous entries outside Reset and Recovery. The existing generic settings service can persist the new keys, so no schema migration is required.

Save behavior is explicit:

- `Immediate` applies safe scalar preferences directly.
- `Staged` requires the owning workflow to expose Apply/Cancel and validate before persistence.
- `RestartRequired` is surfaced as a badge and does not hide the restart requirement.

Section reset uses descriptor defaults and value bindings. It affects only descriptors in the selected category and confirms before applying. Errors remain visible through the app dialog/status path.

## Search and deep links

`ISettingsSearchService` searches titles, descriptions, category titles, and keywords. Results are scored, deterministically ordered, and include a breadcrumb plus the exact setting and category IDs. Both theme views use those IDs to open the category and programmatically focus the matching row.

Documented synonyms include DualSense, PlayStation, Steam, artwork, Pictures, and screenshot. Search history is not required for matching and remains disabled unless explicitly enabled.

## Presentation and navigation

Nexa presents a card-based landing page with frequent settings, category cards, grouped setting pages, badges, and a separate recovery entry. Crossbar presents a distinct gradient surface with a vertical category rail and controller-sized setting rows. Both consume the same descriptors and callbacks; setting values are not duplicated by theme.

The app replaces the former flat Manage flyout settings list with one Settings entry. Existing mature dialogs remain the execution surface behind descriptor routes during this transitional phase. Keyboard, mouse, and semantic controller routing operate while the settings overlay owns input; Search focuses the query box, Confirm invokes the focused button, Cancel closes, and directional actions use spatial focus.

## Current limitation

The declarative catalog does not yet provide a generic editor for every scalar descriptor. Several descriptors route to existing grouped dialogs rather than rendering a dedicated control row. Staged-change prompts and the on-screen keyboard search path require runtime manual verification before this work can be called complete.
