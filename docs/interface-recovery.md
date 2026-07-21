# Interface recovery

Press Ctrl+Shift+R to open the recovery dialog. Recovery is never automatic or silent: it explains scope, requires confirmation, exports a backup, then resets layout, dashboard rows, themes, sounds, and visual performance to safe defaults.

Recovery does not delete library items, providers, play history, Gallery sources/indexes, albums, external programs, artwork, or original media. The default Modern Dashboard is recreated with at least one navigable row, the default theme is applied, and the active projection is refreshed.

If reset fails, the current interface remains open and an actionable status is shown. Startup XAML controls are accessed only after `InitializeComponent` and the `Loaded` event.
