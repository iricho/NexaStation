# Windows application launching

Classic executables and original shortcuts use structured argument lists and the appropriate Windows process or shell activation path. Packaged apps use AUMID activation. Shell items use their parsing identity, and URI targets require an absolute safe scheme; unusual schemes need verified confidence or explicit confirmation.

Tracking is separate from activation. `LaunchedProcess` waits for the process NexaStation started. `ActivatedProcessHint` uses the process returned by Windows when it remains valid. Expected-name/path and foreground-window modes require a matching identity within a bounded timeout. `None` and `ManualReturn` never infer duration. Any uncertain packaged or browser handoff fails safely without playtime inflation.

Desktop control starts only after a reliable process is known and an application-specific opt-in resolves. It stops and releases input before the frontend is restored. Games and emulators retain their existing launch behavior and never receive desktop controller mode.

Phase 6 reuses imported Windows applications as Media Hub players and media-server entries. Media categories (Media, Streaming, Entertainment, Music, Video) are organizational; manual user classification overrides provider suggestions. Preferred media launch uses the application’s validated structured target and never constructs a shell command.
