CREATE TABLE ProviderScanHistory
(
    Id TEXT PRIMARY KEY,
    ProviderId TEXT NOT NULL,
    StartedAtUtc TEXT NOT NULL,
    CompletedAtUtc TEXT NULL,
    Status INTEGER NOT NULL,
    DiscoveredCount INTEGER NOT NULL DEFAULT 0,
    AddedCount INTEGER NOT NULL DEFAULT 0,
    UpdatedCount INTEGER NOT NULL DEFAULT 0,
    MissingCount INTEGER NOT NULL DEFAULT 0,
    FailedCount INTEGER NOT NULL DEFAULT 0,
    WarningCount INTEGER NOT NULL DEFAULT 0,
    ErrorSummary TEXT NULL,
    FOREIGN KEY (ProviderId) REFERENCES Providers(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ProviderScanHistory_ProviderStarted ON ProviderScanHistory(ProviderId, StartedAtUtc DESC);
