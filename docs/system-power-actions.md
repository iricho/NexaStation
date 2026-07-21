# System power actions

Phase 6 exposes Sleep, Sign Out, Restart, and Shut Down through `ISystemPowerService`. Implementations use documented Windows APIs (`SetSuspendState`, `ExitWindowsEx`, and `InitiateSystemShutdownEx`) with force-close disabled. Availability and policy/privilege failures are returned to the UI.

NexaStation does not call `shutdown.exe`, `cmd.exe`, PowerShell, or an arbitrary shell string. It never requests elevation and does not replace or terminate `explorer.exe`.

Window actions are separate: Minimize and Return to Desktop preserve NexaStation in the taskbar; Restore brings it back; Restart NexaStation starts a single replacement process and uses a ten-second restart-attempt guard.
