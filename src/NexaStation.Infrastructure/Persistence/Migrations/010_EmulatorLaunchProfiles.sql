CREATE TABLE EmulatorLaunchProfiles(
 Id TEXT PRIMARY KEY, Name TEXT NOT NULL, EmulatorInstallationId TEXT NOT NULL, PlatformId TEXT NOT NULL,
 ArgumentsTemplate TEXT NOT NULL, WorkingDirectory TEXT NULL, Fullscreen INTEGER NOT NULL DEFAULT 1,
 ExitEmulatorWhenGameStops INTEGER NOT NULL DEFAULT 1, ProcessDetectionMode INTEGER NOT NULL,
 ExpectedProcessName TEXT NULL, AdditionalEnvironmentJson TEXT NULL, UserManaged INTEGER NOT NULL DEFAULT 0,
 CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL,
 FOREIGN KEY(EmulatorInstallationId) REFERENCES EmulatorInstallations(Id) ON DELETE CASCADE,
 FOREIGN KEY(PlatformId) REFERENCES EmulatorPlatforms(Id));

CREATE TABLE RetroArchCores(
 Id TEXT PRIMARY KEY, EmulatorInstallationId TEXT NOT NULL, DisplayName TEXT NOT NULL, CorePath TEXT NOT NULL,
 NormalizedCorePath TEXT NOT NULL, CoreFileName TEXT NOT NULL, SupportedPlatformIdsJson TEXT NOT NULL,
 SupportsNoGame INTEGER NOT NULL DEFAULT 0, Available INTEGER NOT NULL DEFAULT 1, LastValidatedAtUtc TEXT NULL,
 FOREIGN KEY(EmulatorInstallationId) REFERENCES EmulatorInstallations(Id) ON DELETE CASCADE,
 UNIQUE(EmulatorInstallationId,NormalizedCorePath));

ALTER TABLE LaunchTargets ADD COLUMN EmulatedGameSourceId TEXT NULL;
ALTER TABLE LaunchTargets ADD COLUMN EmulatorInstallationId TEXT NULL;
ALTER TABLE LaunchTargets ADD COLUMN LaunchProfileId TEXT NULL;
ALTER TABLE LaunchTargets ADD COLUMN ArgumentListJson TEXT NULL;
ALTER TABLE LaunchTargets ADD COLUMN EnvironmentOverridesJson TEXT NULL;
ALTER TABLE EmulatedGameSources ADD COLUMN RetroArchCoreId TEXT NULL REFERENCES RetroArchCores(Id) ON DELETE SET NULL;
