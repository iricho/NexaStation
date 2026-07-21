CREATE TABLE SmartCollectionRules
(
    Id TEXT PRIMARY KEY,
    CollectionId TEXT NOT NULL,
    RuleGroup INTEGER NOT NULL,
    Field INTEGER NOT NULL,
    Operator INTEGER NOT NULL,
    ValueJson TEXT NULL,
    SortOrder INTEGER NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (CollectionId) REFERENCES Collections(Id) ON DELETE CASCADE
);
