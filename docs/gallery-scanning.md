# Gallery scanning and refresh

Traversal starts at the approved root, remains contained beneath it, and optionally descends into subfolders. Reparse points are skipped to prevent junction/symlink escape and cycles. Hidden/system entries are excluded unless the source explicitly enables hidden files. Access failures are reported and scanning continues where safe.

Supported images are `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp`, `.gif`, `.tif`, and `.tiff`; codec-dependent `.heic`, `.heif`, and `.avif` files are also indexed. Supported videos are `.mp4`, `.m4v`, `.mov`, `.avi`, `.mkv`, `.wmv`, `.webm`, `.mpeg`, and `.mpg`. Indexing an extension does not guarantee that the installed Windows codecs can decode or play it.

A successful refresh transaction upserts discovered files and then marks no-longer-seen rows unavailable. A cancelled, failed, partially inaccessible, or disconnected scan does not mark previously indexed files missing. Refresh preserves favorites, hidden state, albums, and preferred-player choices. Metadata inspection is bounded to four concurrent operations.

Optional `FileSystemWatcher` instances exist only for enabled, opted-in sources. Events are debounced for 750 ms and overflow triggers a full source refresh. Watchers are a convenience, not a durability guarantee; startup/manual refresh remains authoritative.
