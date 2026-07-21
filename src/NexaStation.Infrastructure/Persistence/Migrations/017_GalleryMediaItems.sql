CREATE TABLE GalleryMediaItems(
 Id TEXT PRIMARY KEY, GallerySourceId TEXT NOT NULL, MediaType INTEGER NOT NULL, FilePath TEXT NOT NULL,
 NormalizedFilePath TEXT NOT NULL, FileName TEXT NOT NULL, Extension TEXT NOT NULL, ParentFolder TEXT NOT NULL,
 FileSize INTEGER NOT NULL, CreatedAtUtc TEXT NULL, ModifiedAtUtc TEXT NOT NULL, CapturedAtUtc TEXT NULL,
 Width INTEGER NULL, Height INTEGER NULL, DurationMilliseconds INTEGER NULL, Orientation INTEGER NULL,
 Favorite INTEGER NOT NULL DEFAULT 0, Hidden INTEGER NOT NULL DEFAULT 0, Available INTEGER NOT NULL DEFAULT 1,
 ThumbnailState INTEGER NOT NULL DEFAULT 0, AddedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL, LastOpenedAtUtc TEXT NULL,
 FOREIGN KEY(GallerySourceId) REFERENCES GallerySources(Id) ON DELETE CASCADE,
 UNIQUE(GallerySourceId,NormalizedFilePath));
CREATE INDEX IX_GalleryMediaItems_Source ON GalleryMediaItems(GallerySourceId);
CREATE INDEX IX_GalleryMediaItems_MediaType ON GalleryMediaItems(MediaType);
CREATE INDEX IX_GalleryMediaItems_Captured ON GalleryMediaItems(CapturedAtUtc DESC);
CREATE INDEX IX_GalleryMediaItems_Modified ON GalleryMediaItems(ModifiedAtUtc DESC);
CREATE INDEX IX_GalleryMediaItems_Favorite ON GalleryMediaItems(Favorite);
CREATE INDEX IX_GalleryMediaItems_Hidden ON GalleryMediaItems(Hidden);
CREATE INDEX IX_GalleryMediaItems_Available ON GalleryMediaItems(Available);
CREATE INDEX IX_GalleryMediaItems_LastOpened ON GalleryMediaItems(LastOpenedAtUtc DESC);
CREATE INDEX IX_GalleryMediaItems_ParentFolder ON GalleryMediaItems(ParentFolder);
