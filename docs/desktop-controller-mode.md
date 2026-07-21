# Desktop controller mode

Desktop controller mode converts a gamepad into a deliberately small desktop input surface for an opted-in Windows application. The default Basic Mouse profile maps left stick to cursor movement, right stick to scrolling, A/B to left/right click, shoulders to slow/fast cursor motion, and selected face/view buttons to approved navigation keys.

Cursor movement applies a deadzone, acceleration curve, sensitivity, elapsed-time scaling, and a per-tick clamp. Profiles and per-application assignments are persisted. Applications default to disabled; games and emulators are rejected even if a malformed assignment exists.

Both stick buttons form the emergency-disable chord. Controller disconnect, application exit, frontend restoration, repeated injection failures, cancellation, and shutdown all stop the session and release simulated buttons. Recent physical mouse activity pauses controller cursor injection for two seconds.
