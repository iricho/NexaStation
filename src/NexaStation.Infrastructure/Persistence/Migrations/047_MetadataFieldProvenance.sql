CREATE TABLE MetadataFieldProvenance(
 LibraryItemId TEXT NOT NULL, FieldName TEXT NOT NULL, SourceType INTEGER NOT NULL,
 SourceProviderId TEXT NULL, SourceRecordId TEXT NULL, AppliedAtUtc TEXT NOT NULL,
 Confidence INTEGER NOT NULL, UserModifiedAfterApply INTEGER NOT NULL DEFAULT 0, Locked INTEGER NOT NULL DEFAULT 0,
 PRIMARY KEY(LibraryItemId,FieldName), FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE);
