CREATE TABLE EcosystemCapabilityState(
 CapabilityId TEXT PRIMARY KEY, CapabilityVersion INTEGER NOT NULL, Status INTEGER NOT NULL,
 SnapshotJson TEXT NOT NULL, LastInvestigatedAtUtc TEXT NOT NULL, ExpiresAtUtc TEXT NULL,
 FailureCode TEXT NULL, FailureMessage TEXT NULL, UpdatedAtUtc TEXT NOT NULL);
