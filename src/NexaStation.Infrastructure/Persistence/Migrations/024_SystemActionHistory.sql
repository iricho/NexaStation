CREATE TABLE SystemActionHistory
(
    Id TEXT PRIMARY KEY,
    ActionType INTEGER NOT NULL,
    RequestedAtUtc TEXT NOT NULL,
    CompletedAtUtc TEXT NULL,
    Result INTEGER NOT NULL,
    FailureMessage TEXT NULL
);
