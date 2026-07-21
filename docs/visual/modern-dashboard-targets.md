# Modern Dashboard visual targets

Phase 7.5 makes NexaStation a readable living-room frontend while preserving its own identity. The dashboard should fill the selected display, use the local library as its only content source, and remain useful when artwork is incomplete.

The visual hierarchy is: navigation and status, cinematic hero, truthful game activity, applications and Gallery previews, supporting media/favorites/activity, additional rows, then the persistent input footer. No third-party artwork, logos, completion percentages, download progress, or usage history may be invented.

Acceptance targets:

- Fullscreen `AppWindowPresenterKind.FullScreen`, with the XAML root stretched in both directions.
- Large, readable primary navigation and an obvious active/focused state.
- A 190–230 logical-pixel hero at 720p, 250–300 at 1080p, and approximately 316 at 1440p.
- Artwork-led games, icon-led applications, photographic Gallery cards, and type-specific deterministic fallbacks.
- Horizontal virtualized rows, no eager full-resolution image decoding, and cancellation when a Gallery card is unloaded.
- Contextual top-right notifications that do not cover navigation.
- Full-width, non-overlapping controller/keyboard footer.

Reference captures live in `artifacts/phase75/`. They contain only the user's local data and NexaStation-owned fallback visuals.
