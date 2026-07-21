CREATE TABLE InputGlyphPreferences(
    Id INTEGER PRIMARY KEY CHECK(Id = 1),
    PromptStyle TEXT NOT NULL DEFAULT 'Automatic',
    NintendoConvention TEXT NOT NULL DEFAULT 'PhysicalLabels',
    UpdatedAtUtc TEXT NOT NULL
);
