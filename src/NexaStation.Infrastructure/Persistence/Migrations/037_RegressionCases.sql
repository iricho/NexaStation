CREATE TABLE RegressionCases
(
    Id TEXT PRIMARY KEY,
    Title TEXT NOT NULL,
    Subsystem TEXT NOT NULL,
    SanitizedFixturePath TEXT NOT NULL,
    ReproductionSteps TEXT NOT NULL,
    ExpectedResult TEXT NOT NULL,
    Status INTEGER NOT NULL,
    IntroducedVersion TEXT NOT NULL,
    FixedVersion TEXT NULL,
    UpdatedAtUtc TEXT NOT NULL
);

CREATE INDEX IX_RegressionCases_Status ON RegressionCases(Status, Subsystem);
