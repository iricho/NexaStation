CREATE TABLE Collections
(
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    CollectionType INTEGER NOT NULL,
    IconKey TEXT NULL,
    CoverAssetPath TEXT NULL,
    ParentCollectionId TEXT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    Hidden INTEGER NOT NULL DEFAULT 0,
    MatchMode INTEGER NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (ParentCollectionId) REFERENCES Collections(Id) ON DELETE SET NULL
);
CREATE TABLE CollectionItems
(
    CollectionId TEXT NOT NULL,
    TargetType INTEGER NOT NULL,
    TargetId TEXT NOT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    AddedAtUtc TEXT NOT NULL,
    PRIMARY KEY (CollectionId, TargetType, TargetId),
    FOREIGN KEY (CollectionId) REFERENCES Collections(Id) ON DELETE CASCADE
);
