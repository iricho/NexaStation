CREATE TABLE MediaAssociations
(
    Id TEXT PRIMARY KEY,
    ScopeType INTEGER NOT NULL,
    ScopeValue TEXT NULL,
    MediaType INTEGER NULL,
    FileExtension TEXT NULL,
    PlayerLibraryItemId TEXT NULL,
    LaunchTargetId TEXT NULL,
    ArgumentsTemplate TEXT NULL,
    TrackingMode INTEGER NOT NULL,
    HideFrontend INTEGER NOT NULL DEFAULT 0,
    RestoreFrontend INTEGER NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (PlayerLibraryItemId) REFERENCES LibraryItems(Id) ON DELETE SET NULL
);
CREATE INDEX IX_MediaAssociations_Scope ON MediaAssociations(ScopeType,ScopeValue);
