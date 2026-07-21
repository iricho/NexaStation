# Semantic input actions

UI behavior consumes `InputAction` values instead of hardware buttons. Physical bindings resolve by active context and then fall back to Global, keeping dashboard, dialog, Quick Menu, gallery, text-entry, calibration, and recovery behavior deterministic.

`InputAction.ContextMenu` opens the selected item's shared action surface. The default dashboard controller binding, keyboard Application/Menu key, Shift+F10, mouse right-click, and touch hold converge on that semantic behavior. `ToggleDetails` remains distinct. Footer and dialog hints resolve through the active adaptive glyph set.
