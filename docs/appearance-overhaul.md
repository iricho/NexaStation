# Appearance and Library scrolling

The Nexa dashboard now uses layered midnight surfaces, a teal accent, clearer heading sizes, consistent card spacing, and explicit hover/selected navigation states. Library search and filters sit above an independently scrolling results grid; result counts, refresh, and presentation controls sit below it.

## Scrolling and layout fixes

Library previously inherited Home's disabled vertical scrolling and placed a non-scrolling GridView inside the Home ScrollViewer. The results now occupy a bounded star-sized row outside that ScrollViewer, with GridView scrolling enabled. Home density updates no longer disable scrolling on other sections.

Grid metrics reserve space for padding and the scrollbar, set equal card sizes, and expose the actual column count to directional navigation. Keyboard movement at a grid boundary can leave the grid instead of trapping focus.

Artwork renders as an ImageBrush so a cover's natural dimensions cannot enlarge a card's contents and push its caption outside the visible bounds. Home display sizes are reapplied when refreshed card lists are published.

## Theme behavior

Theme changes update shared brush instances so controls using StaticResource immediately receive the new colors. Surface palettes define background, elevated surfaces, text, muted text, and borders for dark, light, and high-contrast themes. Artwork captions retain their own foreground colors against the dark artwork overlays.

## Verification

- Debug x64 solution build and unpackaged preview publish succeeded using SDK 10.0.401, with package signing disabled locally.
- All 960 test executions across 31 targets passed. The 12 appearance cases cover a bounded Library viewport, responsive card bindings, grid metrics, and text contrast of at least 4.5:1 on the defined surfaces.
- Live Windows preview at 2560 × 1440: verified mouse-wheel scrolling through a 318-item Library, Tab into the results, and End reaching the final item while search and filters remain visible.
- Visually checked Home artwork/captions and Light Room Home and Library surfaces. Corrected media caption colors after the light-theme check.

Physical-controller navigation, Narrator, high-DPI scaling, and installed-package release validation remain manual checks. These changes primarily refresh the Nexa shell and shared controls; the separate Crossbar Settings presentation is not a complete visual redesign.
