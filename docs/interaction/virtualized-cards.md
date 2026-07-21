# Virtualized cards

Targets are refreshed from the currently focused element and its current data context, so recycled containers do not retain a previous item. Stable ids, not visual-container identity, drive restoration after dialogs, refresh, hiding, and removal. The nearest valid focus fallback remains a UI responsibility.
