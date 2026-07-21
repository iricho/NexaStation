CREATE TABLE ExternalMediaPlayers(
    Id TEXT PRIMARY KEY,
    DisplayName TEXT NOT NULL,
    ApplicationLibraryItemId TEXT NULL,
    ExecutablePath TEXT NULL,
    ApplicationIdentity TEXT NULL,
    IconPath TEXT NULL,
    ArgumentsTemplate TEXT NOT NULL DEFAULT '{MEDIA_PATH}',
    SupportedExtensionsJson TEXT NOT NULL DEFAULT '[]',
    DetectionSource TEXT NOT NULL,
    UserAdded INTEGER NOT NULL DEFAULT 0 CHECK(UserAdded IN (0,1)),
    Preferred INTEGER NOT NULL DEFAULT 0 CHECK(Preferred IN (0,1)),
    Enabled INTEGER NOT NULL DEFAULT 1 CHECK(Enabled IN (0,1)),
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX UX_ExternalMediaPlayers_Preferred ON ExternalMediaPlayers(Preferred) WHERE Preferred = 1;
