# Epic Games

Epic discovery reads `.item` JSON files from the normal manifest folder with shared read access. `AppName`/catalog identity provides a stable source and the documented Epic URI activates a game. Discovery does not start Epic or write manifests. Availability depends on locally testable installation metadata.

When online artwork is allowed, newly imported Epic games automatically queue a fill-missing cover and hero search. Exact Epic catalog IDs are sent only to the enabled artwork provider, and existing user-selected artwork is never replaced. The game import still succeeds when artwork is unavailable, offline, or needs review.
