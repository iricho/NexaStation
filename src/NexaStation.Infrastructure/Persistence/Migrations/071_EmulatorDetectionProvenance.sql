CREATE TABLE EmulatorDetectionProvenance(
    CandidateId TEXT NOT NULL REFERENCES EmulatorDiscoveryCandidates(Id) ON DELETE CASCADE,
    SourceId TEXT NULL REFERENCES EmulatorDiscoverySources(Id) ON DELETE SET NULL,
    EvidenceType TEXT NOT NULL,
    EvidenceSummary TEXT NOT NULL,
    DetectedAtUtc TEXT NOT NULL,
    PRIMARY KEY(CandidateId, EvidenceType, DetectedAtUtc)
);
