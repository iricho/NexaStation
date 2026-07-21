# Interface layouts

Phase 7 provides three replaceable presentations over one shared library, Gallery, media history, provider state, favorites, pins, and collections. `IInterfaceLayoutService` persists one active layout and `IInterfacePresentationService` creates exactly one input-owning presentation state. Switching layout never duplicates or migrates library content.

Modern Dashboard uses row navigation, Classic Crossbar uses category-first navigation, and Compact Handheld uses tabs plus a compact grid. Semantic focus stores section, row key, target identity, collection context, layout, and fallback index, then restores the same target or the nearest safe target after a switch.

The Appearance dialog previews layout metadata without querying the library. Apply persists the selection, refreshes the projection, and restores focus. Ctrl+Shift+R offers a confirmed, backup-first reset to Modern Dashboard.
