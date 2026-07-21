CREATE TABLE CompatibilityProfiles
(
    Id TEXT PRIMARY KEY,
    TargetType INTEGER NOT NULL,
    ProviderId TEXT NULL,
    ProviderItemId TEXT NULL,
    PlatformId TEXT NULL,
    EmulatorAdapterId TEXT NULL,
    ExecutableIdentity TEXT NULL,
    ApplicationVersionConstraint TEXT NULL,
    WindowsVersionConstraint TEXT NULL,
    ProfileVersion INTEGER NOT NULL,
    RuleJson TEXT NOT NULL,
    Source INTEGER NOT NULL,
    Enabled INTEGER NOT NULL DEFAULT 1,
    UserModified INTEGER NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);

CREATE INDEX IX_CompatibilityProfiles_ProviderIdentity ON CompatibilityProfiles(ProviderId, ProviderItemId);
CREATE INDEX IX_CompatibilityProfiles_ExecutableIdentity ON CompatibilityProfiles(ExecutableIdentity);
