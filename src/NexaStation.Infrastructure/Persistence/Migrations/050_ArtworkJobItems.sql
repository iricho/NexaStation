CREATE TABLE ArtworkJobItems(
 Id TEXT PRIMARY KEY, ArtworkJobId TEXT NOT NULL, LibraryItemId TEXT NOT NULL, Status INTEGER NOT NULL,
 SelectedProviderId TEXT NULL, SelectedExternalGameId TEXT NULL, MatchConfidence INTEGER NULL,
 RetryCount INTEGER NOT NULL DEFAULT 0, FailureCode TEXT NULL, FailureMessage TEXT NULL, UpdatedAtUtc TEXT NOT NULL,
 FOREIGN KEY(ArtworkJobId) REFERENCES ArtworkJobs(Id) ON DELETE CASCADE,
 FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE,
 UNIQUE(ArtworkJobId,LibraryItemId));
CREATE INDEX IX_ArtworkJobItems_Work ON ArtworkJobItems(ArtworkJobId,Status,UpdatedAtUtc);
