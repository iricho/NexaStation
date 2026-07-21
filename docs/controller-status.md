# Controller status

Controller status uses `Windows.Gaming.Input` and the same Gamepad collection used by NexaStation’s existing input abstraction. It subscribes to connection changes and does not create a second high-frequency controller manager.

The panel reports a session-stable identifier, Windows display name, family, connection state, and player index where available. Battery information is optional. `Unknown` is shown when Windows does not provide a reliable battery report; NexaStation does not infer wired/wireless state or query battery data every frame.

A disconnect cancels any pending destructive-action hold. Navigation remains available when status or battery queries fail.
