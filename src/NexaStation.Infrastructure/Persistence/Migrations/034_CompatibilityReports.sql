CREATE TABLE CompatibilityReports
(
    Id TEXT PRIMARY KEY,
    TargetType INTEGER NOT NULL,
    TargetIdentity TEXT NULL,
    DisplayName TEXT NULL,
    ProviderId TEXT NULL,
    NexaStationVersion TEXT NOT NULL,
    WindowsVersion TEXT NULL,
    Architecture TEXT NULL,
    LaunchResult INTEGER NULL,
    TrackingResult INTEGER NULL,
    RestoreResult INTEGER NULL,
    InputResult INTEGER NULL,
    PerformanceResult INTEGER NULL,
    SanitizedNotes TEXT NULL,
    DiagnosticCorrelationId TEXT NULL,
    CreatedAtUtc TEXT NOT NULL
);

CREATE INDEX IX_CompatibilityReports_Target ON CompatibilityReports(TargetType, TargetIdentity);
CREATE INDEX IX_CompatibilityReports_CreatedAtUtc ON CompatibilityReports(CreatedAtUtc DESC);
