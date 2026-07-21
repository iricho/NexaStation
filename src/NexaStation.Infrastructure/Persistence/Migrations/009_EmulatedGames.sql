CREATE TABLE EmulatedGameSources(
 Id TEXT PRIMARY KEY, LibraryItemId TEXT NOT NULL, PlatformId TEXT NOT NULL, GamePath TEXT NOT NULL,
 NormalizedGamePath TEXT NOT NULL, FileExtension TEXT NULL, FileSize INTEGER NULL, FileModifiedAtUtc TEXT NULL,
 GameIdentifier TEXT NULL, IdentificationConfidence INTEGER NOT NULL DEFAULT 0, Region TEXT NULL, Revision TEXT NULL,
 DiscNumber INTEGER NULL, DiscCount INTEGER NULL, GameSourceId TEXT NULL, PreferredEmulatorInstallationId TEXT NULL,
 LaunchProfileId TEXT NULL, Available INTEGER NOT NULL DEFAULT 1, FirstSeenAtUtc TEXT NOT NULL, LastSeenAtUtc TEXT NOT NULL,
 FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE,
 FOREIGN KEY(PlatformId) REFERENCES EmulatorPlatforms(Id),
 FOREIGN KEY(GameSourceId) REFERENCES GameSources(Id) ON DELETE SET NULL,
 FOREIGN KEY(PreferredEmulatorInstallationId) REFERENCES EmulatorInstallations(Id) ON DELETE SET NULL,
 UNIQUE(PlatformId, NormalizedGamePath));
CREATE INDEX IX_EmulatedGameSources_LibraryItemId ON EmulatedGameSources(LibraryItemId);
CREATE INDEX IX_EmulatedGameSources_GameSourceId ON EmulatedGameSources(GameSourceId);
