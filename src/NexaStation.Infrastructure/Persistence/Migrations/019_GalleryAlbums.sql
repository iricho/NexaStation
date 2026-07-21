CREATE TABLE GalleryAlbums(
 Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Description TEXT NULL, AlbumType INTEGER NOT NULL,
 CoverMediaItemId TEXT NULL, FolderPath TEXT NULL, SortOrder INTEGER NOT NULL DEFAULT 0,
 CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL,
 FOREIGN KEY(CoverMediaItemId) REFERENCES GalleryMediaItems(Id) ON DELETE SET NULL);
CREATE TABLE GalleryAlbumItems(
 GalleryAlbumId TEXT NOT NULL, GalleryMediaItemId TEXT NOT NULL, SortOrder INTEGER NOT NULL DEFAULT 0, AddedAtUtc TEXT NOT NULL,
 PRIMARY KEY(GalleryAlbumId,GalleryMediaItemId),
 FOREIGN KEY(GalleryAlbumId) REFERENCES GalleryAlbums(Id) ON DELETE CASCADE,
 FOREIGN KEY(GalleryMediaItemId) REFERENCES GalleryMediaItems(Id) ON DELETE CASCADE);
CREATE INDEX IX_GalleryAlbumItems_Media ON GalleryAlbumItems(GalleryMediaItemId);
