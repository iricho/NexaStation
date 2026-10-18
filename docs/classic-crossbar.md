# Classic Crossbar

Classic Crossbar is an original category-first presentation inspired by efficient console navigation without copying proprietary art, sounds, or branding. Sections form the category axis and the selected section's primary row forms the item axis.

The layout uses centered icon navigation, a compact featured panel, and a horizontal poster row over a static blue-wave wallpaper. The selected poster grows slightly and has a blue glow; keyboard focus updates the featured panel. Platform badges use imported platform metadata, and applications display their icons without cropping. Item options use the existing context menu. It consumes the same repository, pins, collections, launch coordinator, and semantic focus state as Modern Dashboard.

Home prioritizes recently launched games and applications, then fills the row alphabetically from visible, profile-authorized items. Featured activity and playtime are real library values; no completion percentage is fabricated. The Home footer is hidden to preserve the supplied reference composition, while other Crossbar sections retain their input hints.

The bundled wallpaper is `src/NexaStation.App/Assets/Backgrounds/crossbar-blue-waves.png`. Its generation method and prompt are recorded in [crossbar-background.md](crossbar-background.md).

## Color schemes and branding

Settings → Personalization → Color scheme opens the shared appearance editor. Black & Gold is available across Modern Dashboard, Classic Crossbar, and Handheld layouts, with independently editable primary and secondary accents. The saved palette survives layout changes and application restart. Custom Black & Gold accents retain the warm black surfaces and readable cream text. Crossbar uses matching gold waves for the default gold background and honors custom solid, gradient, and image backgrounds.

The root `icon.png` and `64xicon.png` provide the application branding. Bundled PNGs appear in the shell, a multi-resolution ICO supplies the Windows executable/window icon, and package tile/splash images use the same supplied logo.

## Reference update verification (2026-10-17)

- Debug x64 unpackaged app publish succeeded.
- All 82 presentation, 99 customization, 28 theme parity, and 4 Crossbar theme contract tests passed.
- Inspected the Windows preview at 2560 by 1440, including the final wallpaper, navigation, featured panel, and poster row.
- Visually verified Black & Gold in Crossbar Home and in Modern Dashboard Home and Games/Library, including the supplied branding icon and persistent color selection across layout changes.
- Verified keyboard movement from Tekken 5 to 7-Zip updates the selected poster and featured metadata. Verified the poster options button opens the existing item menu.
- Physical-controller and installed-package validation were not performed in this pass.

When Crossbar is inactive it is collapsed and does not own input. Quick Menu remains available globally and application launch/return restores the semantic target.
