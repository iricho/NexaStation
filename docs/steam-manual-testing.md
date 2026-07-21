# Steam provider manual test checklist

Use fictional fixture tests when Steam is unavailable. Never add real manifests, authentication files, user data, personal paths, or copyrighted cache art to the repository.

When Steam is installed:

1. Launch NexaStation and choose **Import Steam**.
2. Confirm the detected root and every available/unavailable library; choose a root manually if needed.
3. Review New/Already Imported/Possible Duplicate states, clear/select items, and import a small set.
4. Confirm cards appear in Home, Games, Library, Recently Added, and search, then persist after restart.
5. Link one import to a same-title manual item and confirm both launch targets remain in Details.
6. Launch a harmless game. Confirm Steam opens, NexaStation waits for the game process before hiding/counting time, restores on exit, and focuses the same card.
7. If detection times out, verify the actionable message and configure an expected process name; do not count Steam-only waiting.
8. Edit title and cover, refresh, and confirm both overrides survive with favorite/history/playtime/hidden state.
9. If safe, disconnect a secondary library, refresh, and confirm its sources are preserved. Reconnect and confirm recovery.
10. Add/remove a harmless test installation, rescan/import, and confirm incremental states without duplicate App IDs.
11. Inspect provider cache and confirm deterministic copies while Steam cache timestamps/content remain unchanged.

Record Windows/Steam versions, library count, tested App IDs (not manifests/account data), process names, failures, and whether exit was normal. Do not claim real-Steam verification unless every claimed step was observed.
