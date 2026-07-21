# Phase 7.6 manual verification

1. Start NexaStation and open **Manage NexaStation → Refresh Steam** twice.
2. Confirm both scans complete, no SQLite details appear in the toast, and the second refresh does not add a `ProviderLibrarySources` row.
3. Confirm legacy source paths ending in `steamapps` are repaired to their Steam library roots without deleting imported items.
4. Open a Steam game and verify local cover/background artwork resolves when Steam has a valid cache file.
5. Add `<game>-cover.jpg` and `<game>-background.jpg` beside a manual or emulated game and verify the card and hero use them.
6. Open **Apps** and verify Installed, Discover, and category filters remain keyboard/controller focusable.
7. Verify installed catalogue identities show **Open**, Store entries show **Open Store**, and web-only entries show **Open Web App**.
8. Return from Microsoft Store and verify one lightweight application scan runs, the card changes only when an exact identity is found, and no progress is fabricated.
9. In a debugger, open item details and inspect the development-only artwork diagnostic block.
