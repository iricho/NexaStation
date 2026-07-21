CREATE TABLE MediaItemPlayerOverrides(
    Id TEXT PRIMARY KEY,
    ScopeType TEXT NOT NULL,
    ScopeValue TEXT NOT NULL,
    ExternalPlayerId TEXT NULL REFERENCES ExternalMediaPlayers(Id) ON DELETE SET NULL,
    UseNexaStation INTEGER NOT NULL DEFAULT 0 CHECK(UseNexaStation IN (0,1)),
    PreferredAudioTrack TEXT NULL,
    PreferredSubtitleTrack TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    UNIQUE(ScopeType, ScopeValue)
);
