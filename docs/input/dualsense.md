# DualSense controller support

Phase 12 treats DualSense support as a release-blocking controller path.

## Current implementation

- Windows controller navigation is still driven through `Windows.Gaming.Input.Gamepad`.
- NexaStation now supplements the high-level `Gamepad` object with `RawGameController` evidence when Windows exposes it.
- Vendor/product evidence for Sony (`054C`) and the DualSense profile (`0CE6`) resolves to the PlayStation family even when the display name is missing or generic.
- PlayStation prompts can be locked independently of the input backend, so a virtual Xbox representation can still display Cross/Circle/Square/Triangle prompts.
- The default semantic profile maps:
  - Cross / south face button to Confirm.
  - Circle / east face button to Cancel.
  - Square / west face button to Context Menu on dashboard surfaces.
  - Triangle / north face button to Favorite according to the existing NexaStation default.
  - Options/Menu to Options.
  - Create/View to Quick Menu.
  - L1/R1 to previous/next section.
  - L2/R2 to previous/next page.

## What is intentionally not used

NexaStation does not persist controller serial numbers, Bluetooth MAC addresses, raw device paths, or personal hardware identifiers.

The PS/Guide button is not required for navigation and remains unassigned by default.

## Hardware verification status

This document records implementation behavior only. Actual USB, Bluetooth, Steam Input, and DS4Windows behavior must be verified with the physical tester controller before Phase 12 is considered complete.
