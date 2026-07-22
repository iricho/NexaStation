CREATE TABLE IF NOT EXISTS ContentLifecycleStatuses (
    TargetType INTEGER NOT NULL,
    TargetId TEXT NOT NULL,
    Status INTEGER NOT NULL CHECK (Status BETWEEN 1 AND 4),
    UpdatedAtUtc TEXT NOT NULL,
    PRIMARY KEY (TargetType, TargetId)
);

CREATE INDEX IF NOT EXISTS IX_ContentLifecycleStatuses_Status_TargetType
    ON ContentLifecycleStatuses(Status, TargetType, UpdatedAtUtc DESC);
