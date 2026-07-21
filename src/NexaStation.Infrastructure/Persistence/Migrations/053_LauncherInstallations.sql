CREATE TABLE LauncherInstallations(
 Id TEXT PRIMARY KEY, ProviderId TEXT NOT NULL, DisplayName TEXT NOT NULL, InstallationPath TEXT NULL,
 ExecutablePath TEXT NULL, Version TEXT NULL, Available INTEGER NOT NULL DEFAULT 1,
 FirstSeenAtUtc TEXT NOT NULL, LastSeenAtUtc TEXT NOT NULL, LastValidatedAtUtc TEXT NULL,
 UNIQUE(ProviderId,InstallationPath));
CREATE INDEX IX_LauncherInstallations_Provider ON LauncherInstallations(ProviderId,Available);
