CREATE TABLE RemotePlaySessions(
 Id TEXT PRIMARY KEY, ProfileId TEXT NOT NULL, StartedAtUtc TEXT NOT NULL, EndedAtUtc TEXT NULL,
 DurationSeconds INTEGER NULL, Result INTEGER NOT NULL, FailureCode TEXT NULL,
 FOREIGN KEY(ProfileId) REFERENCES RemotePlayProfiles(Id) ON DELETE CASCADE);
CREATE INDEX IX_RemotePlaySessions_ProfileId_Started ON RemotePlaySessions(ProfileId,StartedAtUtc DESC);
