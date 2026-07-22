# UI sounds

Nexa Soft, Minimal Click, Arcade Light, and Silent packs cover navigation, confirm/back, open/close, errors, notifications, startup/shutdown, favorite, and hold completion. The original project-generated PCM cues are cached under NexaStation's centralized data root, including the portable `Data` folder when portable mode is active; no third-party console sounds are used.

Volume and pack selection are local settings. Navigation cues are rate-limited, a missing cue is non-fatal, unknown packs fall back to Silent, and playback is suppressed while an externally tracked application is active. The logical sound service is independent of WinUI and the App supplies the Windows media player.
