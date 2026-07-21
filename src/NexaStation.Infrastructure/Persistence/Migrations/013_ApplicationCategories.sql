CREATE TABLE ApplicationCategories(
 Id TEXT PRIMARY KEY, Name TEXT NOT NULL, SortOrder INTEGER NOT NULL DEFAULT 0, BuiltIn INTEGER NOT NULL DEFAULT 0,
 CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL, UNIQUE(Name));
CREATE TABLE LibraryItemApplicationCategories(
 LibraryItemId TEXT NOT NULL, CategoryId TEXT NOT NULL, IsPrimary INTEGER NOT NULL DEFAULT 0, Source INTEGER NOT NULL,
 PRIMARY KEY(LibraryItemId,CategoryId), FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE,
 FOREIGN KEY(CategoryId) REFERENCES ApplicationCategories(Id) ON DELETE CASCADE);
CREATE UNIQUE INDEX UX_LibraryItemApplicationCategories_Primary ON LibraryItemApplicationCategories(LibraryItemId) WHERE IsPrimary=1;
ALTER TABLE LibraryItems ADD COLUMN ApplicationCardStyle INTEGER NOT NULL DEFAULT 1;

INSERT OR IGNORE INTO ApplicationCategories(Id,Name,SortOrder,BuiltIn,CreatedAtUtc,UpdatedAtUtc) VALUES
 ('entertainment','Entertainment',10,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('media','Media',20,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('communication','Communication',30,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('browser','Browser',40,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('streaming','Streaming',50,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('utilities','Utilities',60,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('system','System',70,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('productivity','Productivity',80,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('development','Development',90,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('creative','Creative',100,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('files','Files',110,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('remote-access','Remote Access',120,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('uncategorized','Uncategorized',999,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now'));
