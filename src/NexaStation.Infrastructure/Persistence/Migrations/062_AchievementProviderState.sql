CREATE TABLE AchievementProviderState(
 ProviderId TEXT PRIMARY KEY, CapabilityStatus INTEGER NOT NULL, AuthenticationType TEXT NOT NULL,
 CanReadDefinitions INTEGER NOT NULL DEFAULT 0, CanReadUserProgress INTEGER NOT NULL DEFAULT 0,
 CanReadUnlockTimes INTEGER NOT NULL DEFAULT 0, RequiresPublicProfile INTEGER NOT NULL DEFAULT 0,
 RequiresDeveloperRegistration INTEGER NOT NULL DEFAULT 0, RequiresUserCredential INTEGER NOT NULL DEFAULT 0,
 SupportsOfflineCache INTEGER NOT NULL DEFAULT 0, LastInvestigatedAtUtc TEXT NOT NULL, Notes TEXT NOT NULL,
 UpdatedAtUtc TEXT NOT NULL);
