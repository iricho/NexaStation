# Gallery thumbnails

Thumbnails are generated lazily when cards or the viewer need them. NexaStation first asks Windows for a local thumbnail, then uses a deterministic local placeholder when decoding is unavailable. Concurrent requests for the same item/size are coalesced.

Generated files live under `%LocalAppData%\NexaStation\Cache\Gallery` (packaged development builds may redirect LocalAppData). Writes use a temporary file followed by an atomic move. SQLite stores only cache path, dimensions, byte size, source fingerprint, and access time.

The decoded in-memory LRU is bounded to 128 MB by default. The disk LRU is bounded to 2 GB by default and evicts least-recently-used generated entries. **Clear Thumbnail Cache** removes only rebuildable NexaStation cache files and records. Source media is never touched.

Thumbnail quality is configurable as a typed setting; Phase 5 ships a balanced default. Video previews depend on Windows thumbnail support and can fall back to a placeholder.
