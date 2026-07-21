CREATE TABLE Providers
(
    Id TEXT PRIMARY KEY,
    DisplayName TEXT NOT NULL,
    Enabled INTEGER NOT NULL DEFAULT 1,
    InstallationPath TEXT NULL,
    SettingsJson TEXT NULL,
    LastSuccessfulScanAtUtc TEXT NULL,
    LastScanStatus INTEGER NULL,
    LastError TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);

CREATE TABLE ProviderLibrarySources
(
    Id TEXT PRIMARY KEY,
    ProviderId TEXT NOT NULL,
    ExternalId TEXT NULL,
    Path TEXT NOT NULL,
    NormalizedPath TEXT NOT NULL,
    Enabled INTEGER NOT NULL DEFAULT 1,
    FirstSeenAtUtc TEXT NOT NULL,
    LastSeenAtUtc TEXT NOT NULL,
    FOREIGN KEY (ProviderId) REFERENCES Providers(Id) ON DELETE CASCADE,
    UNIQUE (ProviderId, NormalizedPath)
);

CREATE TABLE LibraryItemSources
(
    Id TEXT PRIMARY KEY,
    LibraryItemId TEXT NOT NULL,
    ProviderId TEXT NOT NULL,
    ProviderItemId TEXT NOT NULL,
    ProviderLibrarySourceId TEXT NULL,
    InstallationPath TEXT NULL,
    ManifestPath TEXT NULL,
    Installed INTEGER NOT NULL DEFAULT 1,
    ProviderDataJson TEXT NULL,
    FirstSeenAtUtc TEXT NOT NULL,
    LastSeenAtUtc TEXT NOT NULL,
    FOREIGN KEY (LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE,
    FOREIGN KEY (ProviderId) REFERENCES Providers(Id) ON DELETE CASCADE,
    FOREIGN KEY (ProviderLibrarySourceId) REFERENCES ProviderLibrarySources(Id) ON DELETE SET NULL,
    UNIQUE (ProviderId, ProviderItemId)
);

CREATE INDEX IX_ProviderLibrarySources_Provider ON ProviderLibrarySources(ProviderId);
CREATE INDEX IX_LibraryItemSources_Provider ON LibraryItemSources(ProviderId);
CREATE INDEX IX_LibraryItemSources_ProviderItem ON LibraryItemSources(ProviderItemId);
CREATE INDEX IX_LibraryItemSources_Installed ON LibraryItemSources(Installed);
CREATE INDEX IX_LibraryItemSources_LastSeen ON LibraryItemSources(LastSeenAtUtc);
CREATE INDEX IX_LibraryItemSources_Item ON LibraryItemSources(LibraryItemId);

ALTER TABLE LaunchTargets ADD COLUMN ProviderId TEXT NULL;
ALTER TABLE LaunchTargets ADD COLUMN IsEnabled INTEGER NOT NULL DEFAULT 1;
ALTER TABLE LaunchTargets ADD COLUMN UserManaged INTEGER NOT NULL DEFAULT 1;
ALTER TABLE PlaySessions ADD COLUMN ProviderId TEXT NULL;
ALTER TABLE PlaySessions ADD COLUMN ProviderItemId TEXT NULL;
ALTER TABLE PlaySessions ADD COLUMN ProcessNames TEXT NULL;
ALTER TABLE PlaySessions ADD COLUMN DetectionConfidence REAL NULL;

INSERT INTO Providers
    (Id, DisplayName, Enabled, CreatedAtUtc, UpdatedAtUtc)
VALUES
    ('steam', 'Steam', 1, strftime('%Y-%m-%dT%H:%M:%fZ','now'), strftime('%Y-%m-%dT%H:%M:%fZ','now'));

