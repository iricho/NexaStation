CREATE TABLE EmulatorInstallations(
 Id TEXT PRIMARY KEY, AdapterId TEXT NOT NULL, DisplayName TEXT NOT NULL, ExecutablePath TEXT NOT NULL,
 NormalizedExecutablePath TEXT NOT NULL, Version TEXT NULL, Architecture TEXT NULL, InstallationDirectory TEXT NOT NULL,
 UserDataDirectory TEXT NULL, Enabled INTEGER NOT NULL DEFAULT 1, IsPortable INTEGER NOT NULL DEFAULT 0,
 ArgumentsOverride TEXT NULL, CapabilitiesJson TEXT NULL, ValidationStatus INTEGER NOT NULL,
 AddedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL, LastValidatedAtUtc TEXT NULL,
 UNIQUE(AdapterId, NormalizedExecutablePath));
