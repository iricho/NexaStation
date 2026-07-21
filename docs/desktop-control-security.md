# Desktop control security

Desktop control uses `SendInput` only for cursor movement, three mouse buttons, wheel scrolling, and a fixed approved key allowlist. There is no arbitrary text entry, script execution, macro recording, key sequence editor, command shell, clipboard injection, or privilege escalation.

Before starting, NexaStation compares process-token elevation. A non-elevated NexaStation process refuses an elevated target; failure to inspect a target also denies control. Windows itself prevents lower-integrity input from controlling higher-integrity windows, but the explicit check provides a clear product boundary.

The controller loop is cancellable and owns no global hook. It pauses after physical mouse activity, stops after disconnect, and always calls `ReleaseAll` in cleanup. The emergency chord is evaluated before normal actions. External applications and configuration are never modified.
