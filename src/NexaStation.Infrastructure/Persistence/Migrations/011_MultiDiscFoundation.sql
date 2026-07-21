CREATE TABLE GameDiscs(
 Id TEXT PRIMARY KEY, LibraryItemId TEXT NOT NULL, GamePath TEXT NOT NULL, NormalizedGamePath TEXT NOT NULL,
 DiscNumber INTEGER NOT NULL, Label TEXT NULL,
 FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE,
 UNIQUE(LibraryItemId, DiscNumber));
