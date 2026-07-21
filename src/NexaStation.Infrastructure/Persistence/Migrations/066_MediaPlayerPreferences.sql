CREATE TABLE MediaPlayerPreferences(
    Id INTEGER PRIMARY KEY CHECK(Id = 1),
    Volume REAL NOT NULL DEFAULT 1 CHECK(Volume >= 0 AND Volume <= 1),
    Muted INTEGER NOT NULL DEFAULT 0 CHECK(Muted IN (0,1)),
    PlaybackRate REAL NOT NULL DEFAULT 1,
    RememberPlaybackRate INTEGER NOT NULL DEFAULT 0 CHECK(RememberPlaybackRate IN (0,1)),
    AspectRatio TEXT NOT NULL DEFAULT 'Fit',
    ControlsInactivitySeconds REAL NOT NULL DEFAULT 4,
    UpdatedAtUtc TEXT NOT NULL
);
