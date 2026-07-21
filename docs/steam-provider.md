# Steam provider

The Steam provider is local-only. It does not use Steam Web APIs, credentials, cookies, account identifiers, cloud data, or network metadata. It reads installation structure, `libraryfolders.vdf`, `appmanifest_*.acf`, and best-effort local artwork. It never edits Steam files.

## Detection and libraries

Detection checks the persisted user-confirmed folder, current-user registry values, other known machine registry values, then the conventional Program Files location. Every candidate must contain both `steam.exe` and `steamapps`; Steam need not be running. A native folder picker supports manual configuration when automatic detection fails.

The primary installation is always considered. `steamapps/libraryfolders.vdf` adds numeric nested `path` entries and older flat numeric entries. Paths are normalized and deduplicated. A library without `steamapps` remains visible as unavailable; it does not fail other libraries or mark its games missing. A malformed configuration retains the primary library and produces a warning.

## Manifests, import, and refresh

Only top-level `steamapps/appmanifest_*.acf` files are enumerated. The parser reads `appid`, `name`, `installdir`, `buildid`, `LastUpdated`, `SizeOnDisk`, and `StateFlags`; download counters are retained as compact provider data when present. App IDs must be positive, titles non-empty, paths traversal-safe, and manifests directly under `steamapps`. Installation is probed at `steamapps/common/<installdir>`.

Import shows a selective preview with New, Already Imported, Possible Duplicate, Changed, Missing, and Invalid states. New items create one game card, one unique Steam source, a `steam://run/<appid>` target, and cached local artwork in one transaction. A possible duplicate can be linked to the manual card, preserving both launch targets. Reimport matches `steam` + App ID and does not duplicate.

Refresh updates source paths, install state, compact provider data, title/sort title/content type when not overridden, and the managed target. Favorites, hidden state, descriptions, play history, playtime, custom artwork, manual targets, and overrides remain unchanged. A manifest missing after a successful scan of an available library disables only that Steam source/target. Disconnected libraries cause no destructive missing transition. Use Import Steam again to preview newly discovered applications.

## Artwork

Lookup conservatively checks App-ID filenames in local library-cache and user grid roots for PNG, JPEG, and WebP files. Explicit filename patterns win; otherwise dimensions/aspect ratio classify vertical art as cover and wide art as background. Selected files are validated for type/header, size, and dimensions, then copied deterministically to `%LocalAppData%\NexaStation\Cache\Providers\Steam\<AppId>`. Steam sources are never modified. Provider cache is rebuildable; manually selected user art remains overridden.

## Launching and limitations

NexaStation dispatches `steam://run/<appid>`, then polls for new processes. Steam client/helper/overlay processes are ignored. A candidate must match a configured/learned process name or have an executable inside the game installation root. Related candidates discovered during a short grace period are tracked together. A play session begins only after a game process is attached, so Steam-client waiting is excluded where possible. High-confidence rules are stored locally per App ID.

Some games use protected processes, launchers outside the install directory, anti-cheat handoffs, short-lived bootstrap processes, or process trees whose executable paths Windows will not disclose. These may require an expected-process rule or a future running-process chooser. The current failure message offers keep-waiting/choose-process/settings guidance, but Phase 2 does not yet include an interactive process picker.

## Synthetic performance verification

Measured on the development machine on 2026-07-17 using fictional ACF files, bounded concurrency 8, and a warm local filesystem. Values are milliseconds and are observations, not universal targets.

| Manifests | Library config | Enumerate + parse | Existing match | No-change compare | Preview | Artwork lookup |
|---:|---:|---:|---:|---:|---:|---:|
| 100 | 0.138 | 5.140 | 0.003 | 0.005 | 0.014 | 0.146 |
| 500 | 0.283 | 21.835 | 0.108 | 0.151 | 1.356 | 0.147 |
| 1,000 | 0.321 | 40.498 | 0.015 | 0.048 | 0.119 | 0.146 |
| 5,000 | 0.201 | 191.521 | 0.233 | 0.282 | 0.494 | 0.211 |

The probe enumerates manifests without recursively scanning installations, parses files with a maximum of eight concurrent workers, performs dictionary matching/no-change comparison, constructs normalized preview candidates, and runs a best-effort empty artwork lookup. It does not measure WinUI rendering or real-drive latency.

## Privacy, security, and trademark notice

All discovery stays on the machine. Diagnostics summarize sources and failures; manifest contents, credentials, personal paths, and Steam user data are not copied into source control or scan history. Registry and filesystem input is normalized and validated. The provider does not elevate, execute discovered executables during scanning, recursively inspect installations, or write to Steam.

NexaStation is an independent project and is not affiliated with,
endorsed by, or sponsored by Valve Corporation.
Steam and the Steam logo are trademarks of Valve Corporation.
