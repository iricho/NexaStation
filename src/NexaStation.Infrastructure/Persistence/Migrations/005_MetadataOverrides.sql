CREATE TABLE MetadataOverrides
(
    LibraryItemId TEXT NOT NULL,
    FieldName TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    PRIMARY KEY (LibraryItemId, FieldName),
    FOREIGN KEY (LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE
);

