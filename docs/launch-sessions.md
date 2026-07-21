# Launch sessions

A manual executable session starts after Windows successfully returns its process. A provider URI session starts only after `ILaunchedApplicationDetector` attaches a non-client application process. The start transaction stores item/provider identity, UTC start, primary PID, process names, detection confidence, and Last Launched. `Stopwatch` supplies monotonic active elapsed time. When every attached candidate exits, one transaction sets UTC end/duration/normal-exit and increments total playtime exactly once. Validation, URI dispatch, detection waiting, and Steam-client-only time are not counted.

At startup, sessions without an end are marked interrupted. Repaired duration is the non-negative gap from session start to application startup, capped at four hours. Every repair is logged. Accurate cross-restart process recovery and arbitrary process-tree tracking remain postponed. Steam uses bounded new-process polling, expected/learned names, installation-root containment, ignored client processes, and a short related-process grace period.

Web/URI targets use the default Windows handler. Browser processes are shared, so web additions default to no hide and no automatic restoration; no browser credentials or tab state are observed.
# Emulator sessions

Persisted emulator targets contain game-source, emulator-installation, and optional launch-profile IDs rather than a rendered command. At launch time the resolver validates the game, preferred emulator, platform, profile, optional RetroArch core, and template variables, then returns an executable plus argument list and explicit environment overrides.

The directly launched emulator process is the default play-session process. The existing coordinator hides only after process start, completes the session once on exit, restores fullscreen, and requests focus restoration for the original card. No emulator process is terminated automatically.
# Application sessions and tracking

Phase 4 distinguishes game and application activity. Classic applications normally use `LaunchedProcess`; packaged apps use an activation process hint or bounded foreground/path identity; PWA targets default to `None`. Tracking policy, expected identity, timeouts, process ID, and session kind are persisted.

`None`/`ManualReturn` record a zero-duration launch event so Last Launched is accurate without inventing browser-tab or brokered-process time. If Windows accepts activation but no reliable tracked process can be attached, the launch reports the limitation and records no duration. Desktop control is tied to the tracked process lifecycle and always stops before focus/fullscreen restoration.

# Gallery media launching

The built-in viewer opens images without creating play sessions. Video/default-app opening requires a direct user action and delegates to Windows associations. Preferred-player resolution is item, source, then global-video scope and references an existing NexaStation application target. `{MEDIA_PATH}` is rendered as one literal argument without a command shell; shell operators and script hosts are rejected. External-player duration is not inferred when tracking is unreliable.

# Phase 6 current activity and recent media

Quick Menu current activity reuses the tracked launch coordinator when a reliable process exists and otherwise shows the focused NexaStation item or Home. Phase 6 does not add terminate-process controls. Media openings are recorded separately from play sessions only after successful dispatch; no playback duration or “Continue Watching” claim is inferred.
