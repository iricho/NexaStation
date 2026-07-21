CREATE TABLE PerformanceBaselines
(
    Id TEXT PRIMARY KEY,
    Scenario TEXT NOT NULL,
    MachineProfile TEXT NOT NULL,
    ApplicationVersion TEXT NOT NULL,
    MetricName TEXT NOT NULL,
    MetricValue REAL NOT NULL,
    Unit TEXT NOT NULL,
    RecordedAtUtc TEXT NOT NULL
);

CREATE INDEX IX_PerformanceBaselines_Scenario ON PerformanceBaselines(Scenario, MachineProfile, MetricName, RecordedAtUtc DESC);
