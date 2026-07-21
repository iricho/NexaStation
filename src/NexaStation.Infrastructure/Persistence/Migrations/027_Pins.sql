CREATE TABLE Pins
(
    Id TEXT PRIMARY KEY,
    TargetType INTEGER NOT NULL,
    TargetId TEXT NOT NULL,
    PinLocation INTEGER NOT NULL,
    GroupName TEXT NULL,
    SortOrder INTEGER NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    UNIQUE(TargetType, TargetId, PinLocation)
);
