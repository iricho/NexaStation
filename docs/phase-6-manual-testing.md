# Phase 6 manual testing

Use a self-contained x64 or x86 development publish on Windows. Back up the local database first. Do not complete real Restart PC or Shut Down confirmation on a development machine unless that action is intentionally scheduled.

1. Open every primary page, focus a distinct card/control, press Ctrl+Q, close, and verify exact focus and scroll restoration.
2. Repeat with controller Menu/B. Confirm no input is intercepted after another application has foreground focus.
3. Read master volume, adjust by 5% and 1%, toggle mute, then restore the original values.
4. Connect/remove an audio endpoint and controller; verify readable status and safe unavailable/unknown battery behavior.
5. Disconnect/reconnect the network without probing external hosts; verify status updates.
6. Open Media and verify all nine rows, category behavior, local videos/recordings, favorites, and private-source exclusion.
7. Open media with Windows default and a preferred player. Verify success-only recent history, Unicode paths, missing-player failure, and confirmed fallback.
8. Verify Previous, Play/Pause, and Next with an active Windows media session and the no-session state.
9. Verify Minimize, Return to Desktop, Restore, and the guarded NexaStation restart. Explorer must remain running.
10. Verify power confirmations; press/release destructive holds early and trigger focus loss/controller disconnect. The action must cancel.

Automated verification result for this implementation: 351 passed, 0 failed. Hardware/audio/network and real power transitions require the manual steps above and are not claimed by automated tests.
