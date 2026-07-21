# Item actions manual testing

Test Home, Games, Apps, Media, Gallery, Library, Collections, and Discover at 1280×720, 1920×1080, and 2560×1440.

For each applicable card:

1. Open actions using right-click, Application/Menu, Shift+F10, configured controller Context Menu, and touch hold.
2. Verify the first enabled action receives focus, directional navigation stays inside the menu, child Artwork actions return with Back, and Cancel restores the exact card.
3. Verify labels, disabled reasons, active-input glyphs, high contrast, 200% scale, and screen-reader names.
4. Exercise favorite, hide/unhide, edit, collections, artwork, Gallery, and collection actions, then restart and verify persistence.
5. Cancel every destructive confirmation once; verify no state changes. Confirm removal with disposable test records; verify installations, ROMs, Gallery source files, and collection contents remain.
6. For installation management, verify only the provider/Windows settings page opens. Do not claim uninstall success unless a disposable application was intentionally removed and rescanned.
7. Disconnect a controller while the menu is open and verify keyboard/mouse recovery. Test missing files and unavailable providers for explanatory disabled states.
