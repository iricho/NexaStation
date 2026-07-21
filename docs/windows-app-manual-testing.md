# Windows application manual testing

Use a disposable development database and do not import private or work-only entries into screenshots or fixtures.

1. Build and start NexaStation at 720p and 1080p; repeat at 150% scaling.
2. Open **Windows Apps Settings** and verify current-user/common Start Menu, packaged-app, PWA, and startup-refresh toggles persist.
3. Run **Scan Windows Apps**. Verify search, recommended/all-visible/clear, source/status/confidence, category, duplicate action, and desktop-control opt-in.
4. Confirm uninstallers, documentation, related tools, and ordinary website shortcuts are not recommended.
5. Import a classic app, a packaged app, and—if installed—a high-confidence PWA. Confirm Apps grouping, Library presence, details, icon fallback, card-style cycling, category override, and refresh preservation.
6. Launch a classic app and verify frontend restoration and usage duration. Launch a packaged app and confirm uncertain tracking never creates invented duration. PWA duration must remain disabled by default.
7. With a controller, opt in a non-elevated application and verify cursor, left/right click, scroll, slow/fast motion, physical-mouse pause, both-stick emergency disable, disconnect cleanup, and focus restoration.
8. Attempt an elevated target and confirm control is refused. Close the target and NexaStation and verify no simulated button remains pressed.
9. Inspect shortcuts, browser data, installations, and package data to confirm no external file changed.

Record unavailable hardware/apps honestly. Do not claim packaged launch, PWA, controller, scaling, or elevated-target verification unless that environment was actually exercised.
