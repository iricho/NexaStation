CREATE TABLE CapturedMedia(
 Id TEXT PRIMARY KEY, CaptureType INTEGER NOT NULL, LocalPath TEXT NOT NULL, NormalizedLocalPath TEXT NOT NULL,
 TargetLibraryItemId TEXT NULL, ActivitySessionId TEXT NULL, CapturedAtUtc TEXT NOT NULL,
 Width INTEGER NOT NULL, Height INTEGER NOT NULL, FileSize INTEGER NOT NULL, FileFormat INTEGER NOT NULL,
 CaptureSource INTEGER NOT NULL, IncludesCursor INTEGER NOT NULL DEFAULT 0, ImportedToGallery INTEGER NOT NULL DEFAULT 0,
 PrivacyLevel INTEGER NOT NULL, ContentHash TEXT NOT NULL, CreatedAtUtc TEXT NOT NULL,
 FOREIGN KEY(TargetLibraryItemId) REFERENCES LibraryItems(Id) ON DELETE SET NULL,
 UNIQUE(NormalizedLocalPath));
CREATE INDEX IX_CapturedMedia_TargetLibraryItemId ON CapturedMedia(TargetLibraryItemId);
CREATE INDEX IX_CapturedMedia_CapturedAtUtc ON CapturedMedia(CapturedAtUtc DESC);
