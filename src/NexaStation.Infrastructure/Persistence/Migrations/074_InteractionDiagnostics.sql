CREATE TABLE InteractionDiagnostics(
    Id TEXT PRIMARY KEY,
    PageId TEXT NOT NULL,
    ScopeId TEXT NOT NULL,
    ItemType TEXT NULL,
    ItemIdHash TEXT NULL,
    Action TEXT NULL,
    FailureKind TEXT NOT NULL,
    RedactedDetail TEXT NULL,
    OccurredAtUtc TEXT NOT NULL
);
CREATE INDEX IX_InteractionDiagnostics_OccurredAt ON InteractionDiagnostics(OccurredAtUtc DESC);
