# Manual testing

Use a development/test package and never a production user database.

1. Start empty; confirm migrations 1–3 and an empty action state.
2. Add a safe executable, a second app, a web app, a `.lnk`, and local cover art.
3. Favorite one item; restart; verify items, artwork, favorite, section, and selection.
4. Launch the executable; verify hide, process start, restore, Last Launched, playtime, recent row, and focus.
5. Edit and restart; temporarily move the target, verify missing UI, relink, and verify history/art remain.
6. Remove an item and prove its original executable/shortcut/artwork remains.
7. Repeat keyboard/mouse flows; use controller only when hardware is present.
8. Check 1280×720, 1920×1080, and 150% Windows scaling.
9. Record warm startup, dashboard-ready time, idle working set/CPU, and isolated 100/500/1000-item query timings.

Do not claim controller hardware verification without connected hardware. Modern launchers may hand off immediately; record that separately from frontend restoration correctness.
