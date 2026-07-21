# Gallery metadata

The index stores local filesystem facts and lightweight display metadata: path, filename, extension, parent folder, size, created/modified/captured time, dimensions, duration when Windows exposes it, orientation, availability, and source identity. Image metadata can include capture date and camera make/model for diagnostics.

GPS coordinates are not extracted or persisted. Original EXIF/XMP blocks, image pixels, video streams, and file contents are not stored in SQLite. Metadata decoding errors are isolated to the affected item and do not abort a complete source scan.

Windows codec availability varies. HEIC/HEIF, some RAW-derived thumbnails, and uncommon video containers may index successfully but display a placeholder or require an installed codec/player.
