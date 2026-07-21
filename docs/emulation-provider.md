# Emulation provider

The `emulation` provider indexes files only inside folders explicitly selected by the user. Each imported game is a normal `LibraryItem` with a provider source, `EmulatedGameSource`, and an emulator launch target generated at launch time.

NexaStation does not include or provide emulators, games, ROMs, firmware, BIOS, encryption keys, licenses, cores, or artwork downloads. It never installs or updates emulators, modifies emulator configuration, extracts archives, renames game files, or deletes external content. All discovery and launch resolution works offline.

Selection precedence is per-game preference, game-folder preference, platform default adapter, then the first valid compatible installation. An unavailable explicit preference is not silently replaced; fallback requires confirmation. External emulator and platform names belong to their respective owners and do not imply affiliation or endorsement.

Provider refresh updates file metadata and availability only after a successful complete scan. Cancellation, permission failures, and unavailable folders do not mark existing games missing. User metadata, artwork, favorites, playtime, launch overrides, and preferred emulators survive refresh and relinking.
