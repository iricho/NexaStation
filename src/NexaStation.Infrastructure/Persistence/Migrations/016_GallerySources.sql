CREATE TABLE GallerySources(
 Id TEXT PRIMARY KEY, Name TEXT NOT NULL, FolderPath TEXT NOT NULL, NormalizedFolderPath TEXT NOT NULL,
 SourceKind INTEGER NOT NULL, IncludeSubfolders INTEGER NOT NULL DEFAULT 1, Enabled INTEGER NOT NULL DEFAULT 1,
 WatchForChanges INTEGER NOT NULL DEFAULT 0, IncludeHiddenFiles INTEGER NOT NULL DEFAULT 0,
 IncludeImages INTEGER NOT NULL DEFAULT 1, IncludeVideos INTEGER NOT NULL DEFAULT 1, PrivacyLevel INTEGER NOT NULL DEFAULT 0,
 CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL, LastSuccessfulScanAtUtc TEXT NULL,
 UNIQUE(NormalizedFolderPath));
CREATE INDEX IX_GallerySources_Enabled ON GallerySources(Enabled);
CREATE INDEX IX_GallerySources_Privacy ON GallerySources(PrivacyLevel);
