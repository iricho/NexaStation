# Gallery albums

Manual albums store a name, optional description, optional cover-media ID, and ordered media memberships. Creating, renaming, deleting, changing a cover, and adding/removing members are persistence operations only; original folders and media are unchanged.

Smart rows such as Favorites, Recently Added, and Recently Opened are generated from indexed state rather than duplicating media rows. Folder grouping uses the indexed parent folder and source setting. Missing media can remain represented by its unavailable index row, preserving favorite and album intent until it returns or the source index is removed.

Album membership has a unique `(AlbumId, GalleryMediaItemId)` constraint and a stable position index. Private-source items remain private even when included in an album; an album does not override source privacy.
