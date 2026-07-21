# Discover Apps catalogue

Phase 7.6 stores the curated catalogue in `DiscoverApps/discover-apps.v1.json` as an embedded, declarative resource. The parser rejects unknown fields, duplicate IDs, and entries missing identity or display metadata. Catalogue entries cannot execute code.

Installed state is proven only by an exact AUMID, package-family identity, existing `catalogueId` link, verified executable filename at an existing target path, or exact PWA identity. Display names are never installation proof. Store destinations use validated Microsoft Store Product IDs; web destinations are restricted to HTTP or HTTPS.

## Icon provenance

No third-party service logos are bundled in Phase 7.6. Every entry currently has `iconAssetPath: null` and is rendered with a deterministic, original NexaStation generic tile until Windows supplies an installed-app icon or the user selects custom artwork. NexaStation does not scrape, hotlink, or download catalogue icons at runtime.

## Installation policy

Microsoft Store navigation is the supported installation path. NexaStation never automates the Store, never fabricates progress, and rechecks Windows application identities after it regains focus. Direct WinGet installation is intentionally postponed; `Applications.AllowDirectInstallationWithWinget` defaults to `false` and no command shell or `winget.exe` invocation exists in this phase.
