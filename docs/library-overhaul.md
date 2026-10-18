# Library and reliability overhaul

This overhaul improves the library's daily browsing workflow, database loading, Gallery refresh reliability, and local build setup.

## Behavior

- Search operates on a profile-filtered library snapshot. Changing views, sorting, favorites, or refreshing preserves the query. Games and Library share browsing filters; Home and Apps retain their independent content.
- Clear filters resets the query, view, and sorting and returns keyboard focus to search. The selected view also stays synchronized when entering Library from Home's Favorites or Recently Added actions.
- Library shows result counts, loading progress, an empty state, and an inline retry action. Empty results collapse the unused grid. Crossbar updates its results after a search and does not substitute a Home item for an empty library result.
- Cards recalculate their display sizes when the result set changes, including in a one-column viewport. Empty-state messages wrap within their container.
- Dashboard loads all library pages instead of truncating at 1,000 items. Page ordering includes an ID tie-breaker.
- Related launch targets, assets, categories, and desktop-control assignments load in batches of 200. A 1,000-item repository query uses 21 data commands instead of 4,001, excluding connection setup commands. No database migration is required.
- Home resolves pinned and Dynamic Shelf references against the authorized library snapshot, dropping hidden, removed, duplicate, and guest-restricted references while preserving row order.
- Dashboard refreshes are serialized. Gallery search retains the UI synchronization context; Gallery refreshes are serialized and publish completed rows together. Failed loads retain the previous rows and show a retry instruction.

The library snapshot is refreshed after library mutations and through Refresh library. Paging does not provide a transaction spanning the entire dashboard load: a separate writer changing the library between pages can require another refresh. Very large libraries still create card view models for their loaded items; this is not an implementation of full UI data virtualization.

## Automated verification

Verified on 2026-09-20 with SDK 10.0.401: the Debug x64 solution build completed with zero warnings and zero errors; all 948 test executions across 31 targets passed, including 17 added regression cases. `git diff --check` passed.

Run from the repository root:

```powershell
dotnet restore NexaStation.sln -p:Platform=x64
dotnet build NexaStation.sln -c Debug -p:Platform=x64 -p:AppxPackageSigningEnabled=false --no-restore
dotnet test NexaStation.sln -c Debug -p:Platform=x64 --no-build --no-restore
```

Regression tests cover retained search state, literal metadata matching, hidden views, deterministic sorting, reset behavior, disabled launch targets, Home visibility boundaries, paging with tied titles, filtering before paging, batch boundaries, launch arguments and artwork round-trips, empty children, and query cancellation.

The build uses a stable .NET 10 SDK. `global.json` accepts newer 10.0 feature bands through [the documented latestFeature policy](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json#rollforward). Local verification disables package signing because this workspace has no valid signing certificate.

## Manual verification still required

These checks have not been performed for this change:

1. In Nexa and Crossbar, search with a controller, clear the query, change sections, and confirm focus remains usable after the results update.
2. In Nexa, search, change view and sort, favorite an item, refresh, and clear filters. Confirm that Home's content remains unchanged by the query.
3. Resize the window and check results, controls, and empty states at handheld and television sizes, 200% scaling, High Contrast, and with Narrator.
4. Search Gallery rapidly while switching views; confirm there are no cross-thread errors, mixed rows, or stale final results. Test an unavailable media source and retry.
5. Verify pinned and Dynamic Shelf Home rows in Guest Mode and after hiding an item.
6. Run installed-package startup, signing, controller hardware, and long-running memory/CPU checks before treating the changes as release-ready.
