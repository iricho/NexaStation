# Privacy

NexaStation is local-first and has no mandatory telemetry.

## Phase 12 activity, capture, and presence

Activity, captured-media metadata, remote-play profiles, and capability results stay local. Global timeline queries and exports exclude Private, GuestTemporary, and Hidden entries by default; source paths are not exported unless explicitly selected. Presence is disabled by default and is cleared for Guest, Private, Hidden, and restricted content.

NexaStation never captures screenshots automatically and never starts recording. A screenshot requires a direct, intentional action in the current session. Global capture hotkeys are disabled until explicit opt-in. A watcher may observe only new files in folders the user explicitly approves; it treats them as externally created and neither modifies nor uploads them.

It stores library metadata, provider configuration, play sessions, application records, gallery indexes, preferences, backups, logs, and crash reports under `%LocalAppData%\NexaStation`. Artwork, icons, and thumbnails are rebuildable caches. Gallery originals remain in folders selected by the user and are opened read-only; private gallery sources are excluded from Home/global discovery but are not encrypted.

Network access is limited to explicit actions such as opening a web/store destination, invoking a provider-defined URI, or checking a configured alpha update manifest. Steam import uses local files and does not request Steam credentials or the Steam Web API.

Phase 11 online artwork and metadata lookups are disabled by default. When enabled, a provider may receive title and platform; region, serial/title ID, fuzzy lookup, and small-ROM hashing each require their relevant setting. Full disc images are never hashed. Game/ROM paths, launcher manifests, play history, private Gallery data, and the complete library are not sent. Provider secrets stay in Windows Credential Manager; SQLite contains only opaque references.

Crash reports and logs are written locally with user paths, emails, bearer values, and common secret fields redacted. Diagnostics ZIPs exclude the database and original media, are created only on request, and remain local until the user chooses to share them. Review every archive before sharing.

Deleting `%LocalAppData%\NexaStation` after uninstall removes NexaStation-managed local data. It does not delete original games, applications, emulator files, gallery media, or external configuration.

Phase 9 compatibility reports, crash fingerprints, performance baselines, regression cases, and navigation-audit events remain local. Fingerprints contain exception types and normalized NexaStation method names rather than paths, usernames, addresses, filenames, or random IDs. Reproduction bundles exclude the full database; exact executable/ROM/Gallery/provider paths are included only through an explicit consent option. Preparing an issue copies text locally and never submits or uploads it.
Context-menu removal deletes only NexaStation library, Gallery-index, collection, and related local metadata records. It never deletes installations, ROMs, emulators, original artwork, or Gallery source media. Provider entries that remain installed may be rediscovered by a later explicit scan.

## Phase 10 input and profiles

Input mappings, controller-family preferences, display identifiers, calibrations, Guest settings, restrictions, and search preferences stay in the local NexaStation database. Controller serial numbers are neither requested nor stored. Search suggestions are derived locally and exclude private Gallery filenames and personal search history. Owner PINs are stored only as a salted PBKDF2-SHA256 verifier; plaintext PINs are not persisted or logged.

## Phase 13 local media and discovery

Playback progress, track choices, external-player preferences, emulator paths, and discovery provenance stay local. Pictures indexing begins only after visible onboarding confirmation; existing users are not silently enrolled. Emulator discovery is bounded and does not upload paths, scan full drives by default, or download software. Private media paths and titles are excluded from standard diagnostics and public activity surfaces.
