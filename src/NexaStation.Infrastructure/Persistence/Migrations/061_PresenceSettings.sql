CREATE TABLE PresenceSettings(
 Id INTEGER PRIMARY KEY CHECK(Id=1), Provider TEXT NOT NULL DEFAULT 'discord', Enabled INTEGER NOT NULL DEFAULT 0,
 BrowsingPresenceEnabled INTEGER NOT NULL DEFAULT 0, GamePresenceEnabled INTEGER NOT NULL DEFAULT 0,
 IncludeProvider INTEGER NOT NULL DEFAULT 0, IncludePlatform INTEGER NOT NULL DEFAULT 0,
 IncludeElapsedTime INTEGER NOT NULL DEFAULT 0, IncludeButtons INTEGER NOT NULL DEFAULT 0,
 ClearOnGuestMode INTEGER NOT NULL DEFAULT 1, ClearOnPrivateContent INTEGER NOT NULL DEFAULT 1,
 UpdatedAtUtc TEXT NOT NULL);
INSERT INTO PresenceSettings(Id,UpdatedAtUtc) VALUES(1,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
