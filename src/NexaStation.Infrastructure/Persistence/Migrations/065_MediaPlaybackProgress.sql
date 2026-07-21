CREATE TABLE MediaPlaybackProgress(
    MediaItemId TEXT PRIMARY KEY,
    PositionSeconds REAL NOT NULL CHECK(PositionSeconds >= 0),
    DurationSeconds REAL NOT NULL CHECK(DurationSeconds >= 0),
    Completed INTEGER NOT NULL DEFAULT 0 CHECK(Completed IN (0,1)),
    IsPrivate INTEGER NOT NULL DEFAULT 0 CHECK(IsPrivate IN (0,1)),
    LastPlayedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE INDEX IX_MediaPlaybackProgress_LastPlayed ON MediaPlaybackProgress(LastPlayedAtUtc DESC);
