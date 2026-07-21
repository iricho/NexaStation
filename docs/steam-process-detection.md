# Steam process detection

URI dispatch proves only that Windows handed a request to Steam; it does not prove a game is running. `ILaunchedApplicationDetector` therefore separates dispatch from active-session detection.

For a Steam target the detector snapshots existing processes, polls at a bounded interval, ignores known Steam client/helper/overlay names, and considers only new candidates. Expected or learned names receive the highest confidence. Otherwise the executable must be beneath the normalized installation root. After the first match, a short grace window collects related game/helper processes. The coordinator starts persisted playtime at detection, hides NexaStation, waits for all attached candidates, completes the session once, restores fullscreen/foreground, and retains the selected card.

The play-session record stores provider/App ID, primary PID, candidate names, confidence, normal-exit status, and monotonic active duration. A high-confidence name is saved locally for the next launch. No process is killed or injected into.

Detection can fail for access-protected paths, external launchers, anti-cheat transitions, games that reuse an existing process, and late child processes after the grace window. The recovery copy names Keep Waiting, Choose Running Process, and Edit Settings. The interactive chooser remains a documented Phase 2 deviation; users can currently edit/relaunch with an expected process rule.
