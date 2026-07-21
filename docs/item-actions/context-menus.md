# Context menus

`NexaContextMenu` is the shared controller-first action surface. It is modal, constrained to the window safe area, uses large rows, supports one child level, focuses the first action, traps focus while open, and restores the originating control or stable item identity when it closes.

Open it with mouse right-click, touch press-and-hold, the keyboard Application/Menu key, Shift+F10, the remappable semantic Context Menu action, or the configured controller binding. Adaptive glyph text is resolved from the active input family. Disabled actions remain explanatory rather than failing silently.

Phase 13 resolves the menu target from the currently focused actionable card, not `SelectedItem`. Video menus include native play/resume/start-over, Open With, preferred-player recovery, file location, favorite/details, subtitles, progress removal, hiding, and index removal as applicable; source-file deletion is intentionally absent.
