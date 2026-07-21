CREATE TABLE EmulatorDiscoverySources(
    Id TEXT PRIMARY KEY,
    SourceType TEXT NOT NULL,
    NormalizedPath TEXT NULL,
    Enabled INTEGER NOT NULL DEFAULT 1 CHECK(Enabled IN (0,1)),
    UserApproved INTEGER NOT NULL DEFAULT 0 CHECK(UserApproved IN (0,1)),
    LastScannedAtUtc TEXT NULL,
    LastResult TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX UX_EmulatorDiscoverySources_Path ON EmulatorDiscoverySources(SourceType, NormalizedPath);
