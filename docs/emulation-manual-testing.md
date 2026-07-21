# Emulation manual testing

Use only homebrew, demos, or legally obtained user dumps. No real games or emulator binaries belong in the repository.

1. Add each available Dolphin, PCSX2, PPSSPP, and RetroArch installation and review probe diagnostics.
2. Configure an explicit source folder and confirm only expected extensions appear in preview.
3. Import a game, launch it, verify NexaStation hides, exit the emulator, and verify restoration, focus, and playtime.
4. Configure two builds of one emulator and verify the per-game preference.
5. For RetroArch, select a local core and confirm missing-core errors are readable.
6. Rename an emulator executable, use Relink, and confirm games remain intact.
7. Rename a game file, use Relink Game, and confirm ID, artwork, favorite, and history remain intact.
8. Test paths with spaces and Unicode filenames, keyboard/controller navigation, 720p/1080p, and 150% scaling.

The automated suite uses synthetic empty homebrew-style files and fake executable placeholders. It does not prove compatibility with a particular third-party emulator build.
