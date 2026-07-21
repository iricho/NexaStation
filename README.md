# NexaStation

NexaStation is a fullscreen, controller-first Windows frontend with an offline, persistent local library. Phase 12 adds a privacy-filtered local activity timeline, strictly user-initiated screenshot capture, incremental Gallery import, safe remote-play profiles, and an honest cached ecosystem capability matrix. NexaStation never elevates, uploads a library or capture, starts capture/recording automatically, replaces the Windows shell, or modifies external applications, games, emulator configuration, or Gallery media.

Phase 13.5 now has a categorized Settings foundation and isolated built-in Nexa/Crossbar theme contracts. The architecture, independent Settings presentations, preview/persistence/fallback path, and destination parity contracts are implemented, but live Crossbar feature-tree parity and installed runtime verification remain development work. See the [Phase 13.5 report](docs/phase13.5-completion-report.md).

> **Release status:** development preview. Public packaging and distribution are intentionally blocked until the project owner explicitly selects a license. See [LICENSE-DECISION.md](LICENSE-DECISION.md).

Built-in adapters cover Dolphin, PCSX2, PPSSPP, RetroArch, custom emulators, and an experimental manually configured RPCS3 path. Multiple builds of one emulator are supported. See [docs/emulation-provider.md](docs/emulation-provider.md) for scope and safety boundaries.

## Windows application import

Choose **Scan Windows Apps** to scan the current-user/common Start Menu and optional current-user packaged app list. The preview supports search, safe recommended selection, categories, duplicate linking/alternate targets, and per-app desktop-control opt-in. Ordinary website shortcuts, uninstallers, documentation, and maintenance tools are excluded by default. PWA detection uses Windows-visible entries only; browser profiles are never scanned. See [Windows application provider](docs/windows-app-provider.md), [launching](docs/windows-app-launching.md), and [manual testing](docs/windows-app-manual-testing.md).

## Technology and build

- C# / .NET 10, WinUI 3, stable Windows App SDK 2.2
- Microsoft.Data.Sqlite with explicit parameterized SQL and embedded migrations
- CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, xUnit

```powershell
dotnet restore NexaStation.sln
dotnet build NexaStation.sln -c Debug -p:Platform=x64
dotnet test NexaStation.sln -c Debug -p:Platform=x64
```

Open `NexaStation.sln` in Visual Studio, choose the packaged `NexaStation.App` x64 profile, and press F5. Developer Mode is required for loose package registration.

## Using the library

Choose **Add Item** from the header on Home, Games, Apps, Media, or Library. Select Game, Application, Web Application, Media Shortcut, or Custom URI; browse with the native Windows picker for `.exe`/`.lnk` targets or enter a URL/URI; edit metadata and launch behavior; then choose **Save** or **Save and Test**. Executables receive safe product-name, working-directory, and expected-process suggestions. Existing launch-target duplicates produce a warning and are never silently merged.

Right-click or press the context-action control on a card for Details, Favorite, Edit, Hide, or Remove. Missing executable/shortcut targets show a relink action. Removing an item deletes only NexaStation records—never the original executable, shortcut, installation, or artwork file.

Local cover art supports `.png`, `.jpg`, `.jpeg`, and `.webp`. NexaStation stores the original path, preserves the source file, decodes cards near display size, and falls back to the generated card treatment if artwork is absent.

Play sessions are written when a process successfully starts and once again when the tracked process exits. UTC history and monotonic elapsed time update Last Launched and Total Playtime. Interrupted sessions are repaired conservatively and capped at four hours. Browser-tab lifetime and launcher child-process handoffs are not inferred.

## Steam import

Choose **Import Steam** to detect or select a Steam installation, scan configured library folders, review candidates, and selectively import games. Possible duplicates can link to an existing manual card while retaining both targets. **Refresh Steam** updates imported sources without replacing favorites, playtime, hidden state, custom titles, custom artwork, or manual launch behavior. Missing manifests and disconnected drives never delete cards.

Steam import is entirely local and requires no Steam credentials or web API. NexaStation dispatches `steam://run/<AppId>` and begins playtime only after a matching non-Steam game process is detected. See the [Steam provider guide](docs/steam-provider.md) and [manual checklist](docs/steam-manual-testing.md).

## Artwork and other launchers

Use **Metadata and Artwork** to opt into remote providers and save provider keys in Windows Credential Manager. Use **Find Missing Artwork** for durable fill-missing jobs; ambiguous remote matches wait for review and user-selected/locked assets are protected. Exact local files work offline. Use **Scan Other Game Launchers** for read-only Epic, GOG, Ubisoft Connect, EA app, and Battle.net discovery. See [metadata architecture](docs/metadata/architecture.md) and [provider limits](docs/providers.md).

## Data and backup

NexaStation data is centralized under `%LocalAppData%\NexaStation`. The in-app backup action uses SQLite's online backup API, validates the snapshot, records a SHA-256 manifest, and writes a `.nxbackup` archive. A backup is created before schema migration and before restore/reset operations. Do not hand-copy only the live `.db` file while NexaStation is running; use the in-app backup action instead.

See [installation](docs/installation.md), [onboarding](docs/onboarding.md), [recovery](docs/recovery.md), [privacy](PRIVACY.md), and [updates](docs/updates.md).

## Gallery

Choose **Add Gallery Folder** and approve a specific folder, media types, recursion, optional watcher, and privacy level. NexaStation stores paths and lightweight metadata, generates rebuildable thumbnails under `%LocalAppData%\NexaStation\Cache\Gallery`, and leaves every original file untouched. Private sources are omitted from Home and global Gallery discovery, but are not encrypted. Videos open through the Windows default association or a configured preferred player.

See the [Gallery provider](docs/gallery-provider.md), [privacy model](docs/gallery-privacy.md), [viewer](docs/gallery-viewer.md), and [manual checklist](docs/gallery-manual-testing.md).

## Adaptive controls

Prompts follow the most recently meaningful input device, or an explicit prompt lock. Controller labels are text/system-font presentations rather than copied proprietary artwork. Input mapping, prompt style, Nintendo convention, repeat, and deadzone preferences are local and recoverable with `Ctrl+Shift+I`.

Search uses the built-in on-screen keyboard when invoked semantically from a controller. Suggestions come only from non-private local library/application/collection/platform/provider data and are never uploaded.

## Controls

| Input | Action |
|---|---|
| Arrows / W A S D / D-pad / left stick | Navigate |
| Enter / Space / controller A | Confirm / primary action |
| Escape / Back / controller B | Cancel / safe back |
| Q / E / LB / RB | Previous / next section |
| X / controller X | Context action |
| F | Favorite focused item where available |
| F11 | Toggle fullscreen |
| Ctrl+Q / controller Menu | Open or close Quick Menu |
| LB + RB + controller X | Capture NexaStation after this direct action |
| Ctrl+Shift+F12 | Optional global capture action; disabled until explicit opt-in |
| Ctrl+Shift+D | Development exit from fullscreen |
| Ctrl+Shift+R | Confirmed backup-first interface recovery |
| Alt+F4 | Exit NexaStation |

Native file pickers use normal Windows navigation. Dialogs retain focus and return it to the originating or nearest surviving card.

## Current limitations

- Steam detection tracks new installation-root or configured-name processes and a short related-process grace window; protected processes, external launchers, anti-cheat handoffs, and reused processes can still require manual rules.
- Web apps open in the default browser; NexaStation does not embed a browser or detect tab closure.
- Shortcut resolution uses the Windows Script Host COM surface and reports uncertain validation when resolution fails.
- Manual artwork is referenced; selected provider artwork is copied to a rebuildable NexaStation cache. No cropping or downloads are included.
- AppsFolder enumeration uses current-user package app-list entries; packaged process ownership remains best-effort and PWA duration is disabled by default.
- Desktop control has no arbitrary keyboard, text, script, macro, or elevated-window support and requires explicit per-application enablement.
- Gallery codec coverage depends on Windows; video playback is external, private sources are not encrypted, and file watching remains best-effort.
- No cloud sync, media editing, embedded video player, elevation, or silent application management. Alpha update checks are opt-in and use only the configured release manifest.
- Development MSIX output is unsigned. Hardware-controller verification must be performed on a machine with a compatible controller.

## Screenshots

### Home dashboard

![NexaStation Home dashboard showing games, apps, photos, and media services](docs/screenshots/home-dashboard.png)

The fullscreen Home experience brings games, Windows apps, recent photos, media services, and recently added items into one controller-friendly dashboard.

Cards share a controller-first action menu opened by right-click, touch hold, Application/Menu, Shift+F10, or the remappable Context Menu action. Removing an item affects only NexaStation's local index; it never uninstalls software or deletes ROMs or Gallery source files. See [item actions](docs/item-actions/context-menus.md) and [remove versus uninstall](docs/item-actions/remove-vs-uninstall.md).

See [interface layouts](docs/interface-layouts.md), [dashboard customization](docs/dashboard-customization.md), [collections](docs/manual-collections.md), [themes](docs/theme-architecture.md), [interface recovery](docs/interface-recovery.md), [Quick Menu](docs/quick-menu.md), [architecture](docs/architecture.md), [database](docs/database.md), and [Phase 7 manual testing](docs/phase-7-manual-testing.md).

Phase 13 adds a Windows-backed native local-video player with NexaStation controls, local resume progress and safe Open With recovery; bounded emulator discovery; a visible Pictures onboarding proposal; focus-derived action routing; and semantic PlayStation shape assets. See [media](docs/media.md), [emulation](docs/emulation.md), and [focus architecture](docs/navigation/focus-architecture.md).
