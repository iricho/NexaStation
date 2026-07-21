# Emulation process tracking

The default tracking mode is `LaunchedProcess`: NexaStation starts the resolved emulator process directly, confirms launch, starts the play session, hides the frontend, waits for exit, records playtime once, restores fullscreen, and returns focus to the previous card.

Profiles also model process-name, executable-path, child-process, and manual modes for future recovery UI. NexaStation never kills emulator processes automatically. If an emulator remains open after game exit, NexaStation remains hidden until that tracked process closes. Single-instance emulators can reduce tracking accuracy when an existing process accepts the new game.
