# UI sounds

Phase 7 includes Nexa Soft, Minimal Click, Arcade Light, and Silent packs. Event mappings cover navigation, confirm/back, open/close, errors, notifications, startup/shutdown, favorite, and hold completion. Assets are original project-generated short PCM cues materialized only inside NexaStation's LocalAppData sound directory; no third-party console sounds are used.

Volume and pack selection are local settings. Navigation cues are rate-limited, a missing cue is non-fatal, unknown packs fall back to Silent, and playback is suppressed while an externally tracked application is active. The logical sound service is independent of WinUI and the App supplies the Windows media player.
