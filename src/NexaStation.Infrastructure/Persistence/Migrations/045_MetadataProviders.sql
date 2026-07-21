CREATE TABLE MetadataProviders(
 Id TEXT PRIMARY KEY, DisplayName TEXT NOT NULL, Enabled INTEGER NOT NULL DEFAULT 1,
 Priority INTEGER NOT NULL, ConfigurationJson TEXT NULL, LastSuccessfulRequestAtUtc TEXT NULL,
 LastFailureAtUtc TEXT NULL, LastFailureCode TEXT NULL, CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL);
INSERT INTO MetadataProviders(Id,DisplayName,Enabled,Priority,CreatedAtUtc,UpdatedAtUtc) VALUES
 ('local','Local Files',1,0,datetime('now'),datetime('now')),
 ('screenscraper','ScreenScraper',0,10,datetime('now'),datetime('now')),
 ('steamgriddb','SteamGridDB',0,20,datetime('now'),datetime('now')),
 ('thegamesdb','TheGamesDB',0,30,datetime('now'),datetime('now'));
