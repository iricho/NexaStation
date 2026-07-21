# Phase 13 completion report

1. **Phase 12 audit findings:** input was semantic, but MainWindow still inferred an item from focused `DataContext` and several `SelectionMode=None` lists had no selected item. Gallery opened images internally but had no native video session; emulator setup was manual; PlayStation prompts were text.
2. **Exact selection defect:** the white outline was WinUI focus, not selection. Commands consulted a null/stale selection or transient visual `DataContext`, especially after popup transitions and container recycling.
3. **Pages affected:** Home/library game and app rows, Gallery, collections, and Discover were included in the permanent route; all non-selecting card surfaces had the same architectural risk.
4. **Action-target architecture:** `IActionTargetService` stores stable id, kind, page, region, data context, focus/input origins, and timestamp; `IPrimaryActionRouter` executes one registered handler.
5. **Focus versus selection:** keyboard/controller focus defines the actionable item. `SelectedItem` may remain null. Pointer hover is visual only.
6. **Virtualized-card fixes:** the target is refreshed from the currently focused container and current data context and is re-resolved at keypress time.
7. **Focus restoration:** stable ids are retained for return from menus/details/player and existing view-model restoration hooks remain active. Full manual page-by-page verification is pending.
8. **PlayStation assets:** supplied Cross, Circle, Square, Triangle, and D-pad 128 px white PNGs were copied into package-owned assets.
9. **Glyph licensing:** user supplied; no license document was present, so public redistribution clearance is unverified.
10. **Semantic glyph resolution:** action → current binding → physical control → prompt family → asset key → central package URI. High Contrast/missing assets use descriptive text.
11. **Player architecture:** `INexaMediaPlayerService` owns state, session orchestration, preferences, resume, tracks, errors, and lifetime; WinUI observes snapshots.
12. **Playback engine:** Windows `MediaPlayer`, `MediaSource`, `MediaPlaybackItem`, and `MediaPlaybackSession`; no bundled codecs.
13. **Custom controls:** NexaStation top/details and bottom transport overlays around a `MediaPlayerElement` with system transport controls disabled.
14. **Supported controls:** play/pause, stop, seek, 10/30-second skips, queue previous/next, volume, mute, six rates, Fit/Fill/Original/Stretch, tracks, fullscreen, More, and Back.
15. **Resume:** local progress, 30-second minimum, completion at the smaller of final 5% or two minutes, 15-second write throttle, pause/close save, resume/start-over/forget actions.
16. **Subtitles:** embedded tracks exposed by Windows and conservative `.srt`/`.vtt` external or same-folder sidecars; no download or scriptable formats.
17. **Audio tracks:** optional Windows-reported labels/languages, selection without requiring complete metadata.
18. **Fullscreen:** same session and surface, navigation overlay hidden, Escape/Back return, origin focus restoration path implemented.
19. **Unsupported media:** safe failed snapshot with generic message and Retry/Open With/location/details/Back recovery; no codec-pack suggestion.
20. **External-player discovery:** persisted registered players are listed and availability revalidated. Automatic Start Menu/Windows-association population is not implemented.
21. **Open With:** existing executable only, shell executables and chaining rejected, `ArgumentList` used with `UseShellExecute=false`; Windows associations remain unchanged.
22. **Media/Gallery integration:** Gallery videos open the native overlay with privacy and meaningful same-source queue; video context actions include native/resume/start-over/Open With/progress.
23. **Emulator architecture:** definitions and independent bounded sources feed validation, duplicate detection, confidence, preview, and confirmed adapter assignment.
24. **Discovery sources:** existing records, registry uninstall entries, explicit executable, and user-approved portable folders are active. Start Menu, packaged-app, broad installed-app, common-path, and process signals are not active.
25. **Definitions:** Dolphin, PCSX2, PPSSPP, RetroArch, and RPCS3.
26. **Portable behavior:** explicit folders only, no drive roots, depth/file bounds, known names first, reparse points skipped.
27. **Validation:** exact names, existing-file check, installer rejection, product metadata, companion evidence, adapter support, and review threshold.
28. **Duplicates:** normalized executable paths match existing installation ids; confirmed additions do not overwrite existing records.
29. **Platform defaults:** discovered adapters integrate with existing emulation setup; automatic default assignment and full relink UI were not added.
30. **Pictures default:** fresh onboarding proposes Windows Pictures, subfolders on, Home visible, Private off.
31. **Existing-user upgrade:** upgrade state does not silently opt in; a proposal flag is retained. A dedicated one-time upgrade prompt UI remains incomplete.
32. **Opt-out:** Pictures is added and indexed only after completed onboarding with the box selected; normalized duplicates are avoided.
33. **Migrations:** immutable 065–074 add playback progress/preferences, external players/overrides, emulator sources/candidates/provenance, Gallery proposal state, glyph preferences, and interaction diagnostics.
34. **Build:** Debug and Release solution builds succeed with zero reported warnings/errors.
35. **Automated tests:** 834 passed, 0 failed, 0 skipped. New suites contribute 10 interaction, 6 player, and 4 emulator tests, with additional onboarding and glyph assertions.
36. **Manual tests:** runtime UI testing was blocked. Unpackaged launch stops before app code because Windows App Runtime registration is missing (`REGDB_E_CLASSNOTREG`); no NexaStation package is installed.
37. **Media formats tested:** none with real media; synthetic engine tests only.
38. **Subtitle formats tested:** none with real rendering; policy/path behavior only.
39. **External players tested:** none launched; validation and argument rendering were unit tested.
40. **Emulators tested:** no real emulator; synthetic known-name, approved-folder, duplicate/review, and installer-rejection tests.
41. **Controllers tested:** no physical controller; semantic mapping and glyph tests only.
42. **Player latency:** not measured because runtime launch was unavailable.
43. **Repeated-play memory:** not measured.
44. **Discovery performance:** bounded synthetic tests pass; no representative installed-system timing was recorded.
45. **Input-to-action latency:** not measured; routing is in-memory and performs no SQLite lookup per focus move.
46. **Privacy review:** local-only progress/discovery, visible Pictures confirmation, private marker propagation, no uploads/downloads, no hidden source scan.
47. **Security review:** parameterized SQL; no shell; executable/template validation; subtitle format allowlist; bounded scans; installer/reparse rejection; no source/ROM deletion.
48. **Remaining Beta blockers:** runtime/install and hardware testing; real codec/track samples; performance/soak evidence; automatic external-player population; remaining emulator sources/relink/default UX; existing-user prompt; supplied-glyph license proof.
49. **Deviations:** Options/Create/shoulder/trigger image assets were not supplied and use text fallback; installed-app/Start Menu/packaged/common-path emulator sources and automatic external-player discovery are incomplete; several requested manual/performance tests could not be run.
50. **Tester-build readiness:** **No.** The code is build/test clean, but Phase 13 is not ready for a new tester build until the blockers above are resolved and runtime/manual acceptance passes.
51. **Recommended Phase 14:** after closing Phase 13 blockers, focus on localization architecture; Arabic/Hebrew translation; stable theme/layout export; accessibility conformance; installer trust/public signing; Store investigation; update-feed hardening; migration policy; public documentation; compatibility fixes; and stable-scope freeze. No Phase 14 work was implemented here.
