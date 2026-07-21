CREATE TABLE InputBindings
(
    Id TEXT PRIMARY KEY,
    InputProfileId TEXT NOT NULL,
    InputAction INTEGER NOT NULL,
    PhysicalInputJson TEXT NOT NULL,
    Context INTEGER NOT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (InputProfileId) REFERENCES InputProfiles(Id) ON DELETE CASCADE
);
CREATE INDEX IX_InputBindings_ProfileContext ON InputBindings(InputProfileId,Context,SortOrder);
