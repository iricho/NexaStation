# Default theme reference update

The supplied Home and Games screenshots guide the native Nexa default theme. Content, counts, artwork, and metadata come from the user's library.

Home has a compact navigation header, featured banner, four shortcuts, six landscape continue-playing cards at desktop widths, and a lower section combining apps/media with recent additions and tips. The navy surfaces and teal accent follow the references. Layout scaling accounts for larger desktop windows while retaining the configured interface scale.

Games and Library have source/view/category navigation, search and sorting, source pills, a responsive landscape grid, and a selected-item detail panel. Cards expose favorite, play, and options actions. Search and filters remain fixed while results scroll; the detail panel and sidebar collapse at narrower widths. Additional library views and alternate card presentations remain available.

## Verification on 2026-10-17

- Debug x64 unpackaged publish succeeded at `artifacts/reference-final/NexaStation.App.exe`.
- Presentation tests: 80 passed. Customization tests: 97 passed.
- `git diff --check` passed.
- Inspected Home and Games in the Windows preview at 2560 by 1440, including the five-column grid, selected details, readable card captions, footer, and a scrolled results viewport with fixed surrounding controls.
- Verified the search result for Apex Legends in the preview during this update.

Physical-controller navigation and installed-package validation were not performed during this reference update. Earlier overhaul verification is recorded separately in the appearance and library documents.
