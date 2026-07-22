# Onboarding

NexaStation starts with a five-screen, controller-friendly setup for profile and appearance, games and applications, Gallery and system choices, and a final review.

Every setup choice begins unanswered. Continue stays disabled until the user chooses **Set up**, **Not now**, or an equivalent explicit option for every relevant item. Selecting Set up starts a visible local scan and shows a short candidate preview; failed scans must be retried or explicitly skipped. Nothing is scanned merely because a screen was opened.

The review screen lists the chosen appearance, every approved source, every opt-out, and the privacy effect of Gallery and diagnostics choices. Changes begin only after **Apply these choices to this PC** is checked. Progress and failures remain visible, successful tasks are kept, and unresolved failures require explicit acceptance before Home opens.

Setup state uses schema 2 and resumes after an interrupted first launch. Schema-1 states are migrated into the nearest new screen without losing Completed or Skipped status. Existing installations see the full flow, but existing library data and settings remain unchanged unless the review shows a requested change.

The Setup Assistant can also be opened from **Startup, Recovery and Diagnostics**. That version uses a temporary draft: closing it discards the draft and does not make onboarding run at the next startup.

Steam, supported launcher, Windows application, and emulator discovery use bounded, read-only local APIs. Gallery adds only explicitly approved sources. NexaStation never changes launcher credentials, emulator configuration, applications, ROMs, or source media files.
