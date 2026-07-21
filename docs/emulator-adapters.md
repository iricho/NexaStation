# Emulator adapters

`IEmulatorAdapter` owns emulator-specific safe probes, platform compatibility, validation, identification hooks, and launch argument generation. It has no SQL or WinUI dependency and cannot download content or write emulator configuration.

- Dolphin: GameCube/Wii, direct launch, batch mode, optional user directory.
- PCSX2: PlayStation 2, fullscreen/batch profile, explicit game argument separator. Fast boot is not enabled by default.
- PPSSPP: PSP, fullscreen and pause-menu-exit. Escape-exit is optional and not forced.
- RetroArch: requires a user-selected local core and passes core/game as separate arguments.
- RPCS3: experimental manual executable and user-confirmed template only; no installed-title, firmware, package, patch, or license discovery.
- Custom: recognized template variables and safe argument tokenization without scripts or shell evaluation.

Forks and unusual builds can use an installation arguments override or user-managed launch profile.
