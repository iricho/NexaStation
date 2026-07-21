# Item action architecture

Phase 11.5 centralizes contextual actions behind `IItemContextMenuService`. A page supplies an `ItemContextRequest`; ordered `IItemActionProvider` implementations return capability-filtered `ItemActionDescriptor` values. The service validates, deduplicates, groups, and sorts them, and rejects execution of IDs that are not present in the current request.

Descriptors contain presentation metadata and structured execution data only. They cannot contain delegates, command lines, scripts, uninstall strings, or arbitrary executable payloads. WinUI owns confirmation, file pickers, navigation, and presentation; repositories retain ownership of durable mutations.

The current provider covers library games, emulated games, applications, Gallery media, collections, and Discover/streaming entries. New providers should be narrow, deterministic, side-effect free during discovery, and explicit about disabled reasons.
