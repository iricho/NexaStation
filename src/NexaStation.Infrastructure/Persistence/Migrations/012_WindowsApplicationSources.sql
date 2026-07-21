CREATE TABLE WindowsApplicationSources(
 Id TEXT PRIMARY KEY, LibraryItemId TEXT NOT NULL, ProviderId TEXT NOT NULL, ProviderItemId TEXT NOT NULL,
 SourceType INTEGER NOT NULL, ShortcutPath TEXT NULL, NormalizedShortcutPath TEXT NULL,
 TargetPath TEXT NULL, NormalizedTargetPath TEXT NULL, Arguments TEXT NULL, WorkingDirectory TEXT NULL,
 AppUserModelId TEXT NULL, PackageFamilyName TEXT NULL, PackageFullName TEXT NULL, ApplicationId TEXT NULL,
 ProtocolUri TEXT NULL, BrowserFamily TEXT NULL, BrowserAppId TEXT NULL, WebAppUrl TEXT NULL,
 StartMenuFolder TEXT NULL, CategoryHint TEXT NULL, DiscoveryConfidence INTEGER NOT NULL,
 Installed INTEGER NOT NULL DEFAULT 1, ProviderDataJson TEXT NULL, FirstSeenAtUtc TEXT NOT NULL, LastSeenAtUtc TEXT NOT NULL,
 FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE,
 FOREIGN KEY(ProviderId) REFERENCES Providers(Id) ON DELETE CASCADE,
 UNIQUE(ProviderId,ProviderItemId));
CREATE INDEX IX_WindowsApplicationSources_Aumid ON WindowsApplicationSources(AppUserModelId);
CREATE INDEX IX_WindowsApplicationSources_Target ON WindowsApplicationSources(NormalizedTargetPath);
CREATE INDEX IX_WindowsApplicationSources_Shortcut ON WindowsApplicationSources(NormalizedShortcutPath);
CREATE INDEX IX_WindowsApplicationSources_Package ON WindowsApplicationSources(PackageFamilyName);
CREATE INDEX IX_WindowsApplicationSources_BrowserApp ON WindowsApplicationSources(BrowserAppId);
CREATE INDEX IX_WindowsApplicationSources_Installed ON WindowsApplicationSources(Installed);
CREATE INDEX IX_WindowsApplicationSources_Item ON WindowsApplicationSources(LibraryItemId);

ALTER TABLE LaunchTargets ADD COLUMN WindowsApplicationSourceId TEXT NULL REFERENCES WindowsApplicationSources(Id) ON DELETE SET NULL;
ALTER TABLE LaunchTargets ADD COLUMN WindowsLaunchMethod INTEGER NULL;
ALTER TABLE LaunchTargets ADD COLUMN AppUserModelId TEXT NULL;
ALTER TABLE LaunchTargets ADD COLUMN ShellParsingPath TEXT NULL;

INSERT OR IGNORE INTO Providers(Id,DisplayName,Enabled,CreatedAtUtc,UpdatedAtUtc) VALUES
 ('windows-start-menu','Windows Start Menu',1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('windows-apps-folder','Windows Apps',1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('windows-pwa','Installed Web Applications',1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now'));
