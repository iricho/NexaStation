# Gallery privacy and safety

NexaStation indexes only folders selected in the native folder picker and confirmed in the privacy review. It does not perform whole-profile discovery and does not upload media, metadata, thumbnails, or filenames.

Private sources are excluded from the Home preview and Gallery-wide recent/search queries unless the user explicitly navigates to that source or an applicable private context. Private status is a local presentation boundary, not encryption or an operating-system access-control boundary. Anyone who can access the Windows account, database, cache directory, or original folder may still see paths or thumbnails.

The database contains paths and lightweight metadata, never media blobs. GPS is not persisted. The cache contains generated derivatives and can be cleared from Gallery settings. Database/cache backups should be protected like local photo-library metadata.

Scans open source files read-only. Removing a card, album, or source cannot delete, move, rename, edit, quarantine, or upload an external file. Path containment and reparse-point skipping prevent traversal outside an approved root.
