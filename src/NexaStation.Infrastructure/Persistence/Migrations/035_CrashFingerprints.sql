CREATE TABLE CrashFingerprints
(
    Fingerprint TEXT PRIMARY KEY,
    ExceptionType TEXT NOT NULL,
    NormalizedTopFrames TEXT NOT NULL,
    FirstSeenAtUtc TEXT NOT NULL,
    LastSeenAtUtc TEXT NOT NULL,
    OccurrenceCount INTEGER NOT NULL,
    LastApplicationVersion TEXT NOT NULL,
    StartupStage INTEGER NULL,
    ResolvedVersion TEXT NULL
);

CREATE INDEX IX_CrashFingerprints_LastSeenAtUtc ON CrashFingerprints(LastSeenAtUtc DESC);
