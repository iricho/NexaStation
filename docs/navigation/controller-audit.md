# Phase 12 controller audit

## Findings from code inspection

- The active input path was `Windows.Gaming.Input.Gamepad` only.
- The previous family detection path depended mostly on display-name tokens from `RawGameController.FromGameController(gamepad)?.DisplayName`.
- If Windows exposed a DualSense as a generic gamepad without a Sony/DualSense display name, NexaStation could classify it as Generic.
- The controller status/header path had the same display-name-only weakness, so the header/Quick Menu could disagree with the intended PlayStation glyph behavior.
- L2/R2 trigger edges were not emitted as semantic inputs, despite default bindings existing for previous/next page.
- Stick-button inputs were not emitted, so the reserved emergency controller reset chord could not be produced by the Windows gamepad path.
- Controller chords existed in bindings, but the polling service did not emit chord physical inputs.
- The earlier pre-change full test run passed, but the first parallel build collided with active `testhost` processes and failed on locked test DLL copies. A subsequent sequential Debug build passed.

## Implemented in this pass

- Added a controller-device resolver and normalized backend-device model.
- Added built-in DualSense/DualShock/Xbox profile metadata without storing personal hardware identifiers.
- Added Sony vendor/product evidence to family detection.
- Added physical-versus-virtual duplicate suppression for shared physical evidence.
- Added trigger edge emission for L2/R2 style navigation.
- Added stick-button emission for emergency reset support.
- Added dashboard-scoped controller capture chord emission; this still requires a focused current-session user action and does not create automatic capture.
- Updated the controller status service to use the same resolver evidence as the input path.
- Expanded the Input and Controller Mapping UI to expose triggers, stick buttons, and controller chords.

## Not verified from this coding session

- Actual DualSense USB behavior.
- Actual DualSense Bluetooth behavior.
- Steam Input virtual-controller behavior.
- DS4Windows/ViGEm duplicate behavior.
- Xbox-compatible controller hardware behavior.
- Real controller-only navigation across every page.
- Real input latency and idle CPU measurements.

These remain manual tester-matrix requirements before Phase 12 can be called complete.
