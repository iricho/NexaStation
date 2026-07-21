# Quick Menu

The Quick Menu is a lightweight WinUI overlay available from every primary NexaStation page with **Ctrl+Q** or the controller **Menu** button. It is intentionally active only while NexaStation has foreground focus, so controller input is never intercepted from a launched game or application.

Opening captures the current page, item, control, row/scroll state, and exact focused control. The underlying page is not rebuilt. Closing restores that control when it still exists and otherwise restores the nearest remembered card. Duplicate opens are ignored, status refresh is asynchronous, and no provider or Gallery scan runs during open.

The overlay exposes master volume/mute, the current audio output, Windows media-session controls, controller and network status, local time, current activity, window actions, local notifications, and guarded power actions. Fast-changing refresh work is canceled when the menu closes.

Phase 12 adds deliberate **Capture Screenshot** and **Snipping Tool** actions. Each press is a new direct action; opening the menu, launching/exiting content, controller connection, and background refresh never capture. Success appears only after validation and atomic storage. Remote-play/presence capability remains cached and failures do not block the menu.

Restart and shutdown use a 1.5-second hold after confirmation. Releasing the button, changing focus, losing foreground, or disconnecting the controller cancels the hold.

Phase 7 keeps one Quick Menu instance above all three interface layouts. Switching Modern Dashboard, Classic Crossbar, or Compact Handheld does not rebuild the overlay or register duplicate input subscriptions. Opening and closing retains the originating semantic target when possible and otherwise uses the nearest safe focus fallback.
