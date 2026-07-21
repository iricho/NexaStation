# Provider contracts

`IArtworkProvider` exposes status, structured search, asset enumeration, and validated download. `IGameMetadataProvider` exposes structured metadata records. Providers must use stable IDs, HTTPS for remote assets, bounded result counts, and the shared rate coordinator. They may not scrape HTML, launch software during discovery, access launcher credentials, or modify a launcher manifest.
