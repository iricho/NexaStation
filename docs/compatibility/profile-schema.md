# Profile schema

Phase 11 provider IDs (`epic`, `gog`, `ubisoft`, `ea`, `battlenet`, and packaged Windows identities) may be used only as stable match evidence. Compatibility profiles cannot contain provider secrets, remote artwork URLs, executable scripts, launcher configuration mutations, or metadata-provider responses. Multi-provider games keep one profile resolution per selected launch target.

The contribution schema is `compatibility/schema/compatibility-profile.v1.schema.json`. A profile contains a stable target matcher, optional validated version constraints, typed launch/tracking/window/input/artwork/performance rules, source, version, reproduction steps, verification result, and contributor notes.

SQLite stores the complete validated profile JSON plus indexed identity columns. User-modified rows are never replaced by a feed refresh.
