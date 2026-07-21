CREATE TABLE ArtworkAssignments(
 Id TEXT PRIMARY KEY, LibraryItemId TEXT NOT NULL, AssetType INTEGER NOT NULL, LocalPath TEXT NOT NULL,
 ContentHash TEXT NOT NULL, SourceProviderId TEXT NULL, SourceGameId TEXT NULL, SourceAssetId TEXT NULL,
 SourceUrlHash TEXT NULL, OriginalWidth INTEGER NULL, OriginalHeight INTEGER NULL,
 DownloadedWidth INTEGER NULL, DownloadedHeight INTEGER NULL, FileSize INTEGER NOT NULL,
 Confidence INTEGER NOT NULL, UserSelected INTEGER NOT NULL DEFAULT 0, Locked INTEGER NOT NULL DEFAULT 0,
 AttributionJson TEXT NULL, LicenceMetadata TEXT NULL, AppliedAtUtc TEXT NOT NULL,
 IsCurrent INTEGER NOT NULL DEFAULT 1, HistoryPinned INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE);
CREATE INDEX IX_ArtworkAssignments_Current ON ArtworkAssignments(LibraryItemId,AssetType,IsCurrent,AppliedAtUtc);
CREATE UNIQUE INDEX UX_ArtworkAssignments_Current ON ArtworkAssignments(LibraryItemId,AssetType) WHERE IsCurrent=1;
CREATE INDEX IX_ArtworkAssignments_Hash ON ArtworkAssignments(ContentHash);
