CREATE TABLE InputProfiles
(
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    DeviceScope INTEGER NOT NULL,
    DeviceMatchJson TEXT NULL,
    GlyphSetId TEXT NULL,
    Enabled INTEGER NOT NULL DEFAULT 1,
    BuiltIn INTEGER NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);

INSERT INTO InputProfiles(Id,Name,DeviceScope,DeviceMatchJson,GlyphSetId,Enabled,BuiltIn,CreatedAtUtc,UpdatedAtUtc)
VALUES('builtin.default','NexaStation Default',0,NULL,NULL,1,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now'));
