# RetroArch cores

RetroArch launches require an available RetroArch executable, an explicitly selected `*_libretro.dll`, a platform assignment, and a valid game path. Core discovery is limited to the selected core directory and never downloads, updates, or modifies cores or their configuration.

Core and game paths are separate `ProcessStartInfo.ArgumentList` values. Per-game core choice is persisted independently from the rendered command. A missing core produces a readable launch error and is never silently replaced.
