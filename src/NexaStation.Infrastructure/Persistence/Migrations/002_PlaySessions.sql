CREATE TABLE PlaySessions (Id TEXT PRIMARY KEY, LibraryItemId TEXT NOT NULL, StartedAtUtc TEXT NOT NULL, EndedAtUtc TEXT NULL, DurationSeconds INTEGER NULL CHECK(DurationSeconds IS NULL OR DurationSeconds >= 0), ProcessId INTEGER NULL, ExitDetectedNormally INTEGER NOT NULL DEFAULT 0, FailureMessage TEXT NULL, FOREIGN KEY (LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE);
CREATE INDEX IX_PlaySessions_Item ON PlaySessions(LibraryItemId);
CREATE INDEX IX_PlaySessions_Started ON PlaySessions(StartedAtUtc DESC);
