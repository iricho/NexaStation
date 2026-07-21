CREATE TABLE ContentRestrictions
(
    TargetId TEXT NOT NULL,
    TargetKind INTEGER NOT NULL,
    RestrictionState INTEGER NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    PRIMARY KEY(TargetId,TargetKind)
);
CREATE TABLE OwnerPinCredential
(
    Id TEXT PRIMARY KEY CHECK(Id='owner'),
    Salt BLOB NOT NULL,
    Hash BLOB NOT NULL,
    Iterations INTEGER NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE INDEX IX_ContentRestrictions_State ON ContentRestrictions(RestrictionState,TargetKind);
