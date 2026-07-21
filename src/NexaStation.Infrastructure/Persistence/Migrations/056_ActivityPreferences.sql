CREATE TABLE ActivityPreferences(
 Id INTEGER PRIMARY KEY CHECK(Id=1), HistoryMode INTEGER NOT NULL DEFAULT 3,
 RetentionDays INTEGER NOT NULL DEFAULT 365, MaximumEventCount INTEGER NOT NULL DEFAULT 20000,
 KeepGameSessions INTEGER NOT NULL DEFAULT 1, KeepCaptureEvents INTEGER NOT NULL DEFAULT 1,
 KeepArtworkChanges INTEGER NOT NULL DEFAULT 0, LastRetentionAtUtc TEXT NULL, UpdatedAtUtc TEXT NOT NULL);
INSERT INTO ActivityPreferences(Id,HistoryMode,RetentionDays,MaximumEventCount,KeepGameSessions,KeepCaptureEvents,KeepArtworkChanges,UpdatedAtUtc)
VALUES(1,3,365,20000,1,1,0,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
