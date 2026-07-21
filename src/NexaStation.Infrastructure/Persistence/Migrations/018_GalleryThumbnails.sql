CREATE TABLE GalleryThumbnails(
 Id TEXT PRIMARY KEY, GalleryMediaItemId TEXT NOT NULL, ThumbnailType INTEGER NOT NULL, LocalCachePath TEXT NOT NULL,
 Width INTEGER NOT NULL, Height INTEGER NOT NULL, FileSize INTEGER NOT NULL, SourceModifiedAtUtc TEXT NOT NULL,
 CreatedAtUtc TEXT NOT NULL, LastAccessedAtUtc TEXT NOT NULL, GenerationVersion INTEGER NOT NULL,
 FOREIGN KEY(GalleryMediaItemId) REFERENCES GalleryMediaItems(Id) ON DELETE CASCADE,
 UNIQUE(GalleryMediaItemId,ThumbnailType));
CREATE INDEX IX_GalleryThumbnails_LastAccessed ON GalleryThumbnails(LastAccessedAtUtc);
