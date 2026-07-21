CREATE TABLE ProviderDiscoveryState(
 ProviderId TEXT PRIMARY KEY, Status INTEGER NOT NULL, LastStartedAtUtc TEXT NULL, LastCompletedAtUtc TEXT NULL,
 LastSuccessfulAtUtc TEXT NULL, LastFailureCode TEXT NULL, LastFailureMessage TEXT NULL,
 DiscoveredCount INTEGER NOT NULL DEFAULT 0, WarningCount INTEGER NOT NULL DEFAULT 0,
 ConfigurationFingerprint TEXT NULL, UpdatedAtUtc TEXT NOT NULL);
