CREATE TABLE EmulatorDiscoveryCandidates(
    Id TEXT PRIMARY KEY,
    DefinitionId TEXT NOT NULL,
    ExecutablePath TEXT NOT NULL,
    NormalizedExecutablePath TEXT NOT NULL,
    Version TEXT NULL,
    DetectionSource TEXT NOT NULL,
    Confidence REAL NOT NULL CHECK(Confidence >= 0 AND Confidence <= 1),
    Status TEXT NOT NULL,
    ExistingEmulatorId TEXT NULL REFERENCES EmulatorInstallations(Id) ON DELETE SET NULL,
    FirstDetectedAtUtc TEXT NOT NULL,
    LastDetectedAtUtc TEXT NOT NULL,
    UNIQUE(DefinitionId, NormalizedExecutablePath)
);
