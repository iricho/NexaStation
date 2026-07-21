CREATE TABLE FrontendProfiles
(
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    ProfileType INTEGER NOT NULL,
    AvatarKey TEXT NULL,
    ThemeOverrideId TEXT NULL,
    LayoutOverrideId TEXT NULL,
    IsDefault INTEGER NOT NULL DEFAULT 0,
    PinRequired INTEGER NOT NULL DEFAULT 0,
    Enabled INTEGER NOT NULL DEFAULT 1,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE TABLE GuestModeSettings
(
    Id TEXT PRIMARY KEY CHECK(Id='local'),
    SettingsJson TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE TABLE GuestSessionActivity
(
    Id TEXT PRIMARY KEY,
    ActivityType TEXT NOT NULL,
    TargetId TEXT NULL,
    CreatedAtUtc TEXT NOT NULL
);
INSERT INTO FrontendProfiles(Id,Name,ProfileType,AvatarKey,IsDefault,PinRequired,Enabled,CreatedAtUtc,UpdatedAtUtc)
VALUES('owner','Owner',0,'owner',1,0,1,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now'));
INSERT INTO GuestModeSettings(Id,SettingsJson,UpdatedAtUtc)
VALUES('local','{"Enabled":false,"GuestName":"Guest","AllowGames":true,"AllowApplications":true,"AllowGallery":true,"AllowMedia":true,"AllowDiscoverApps":true,"AllowDesktopExit":true,"AllowQuickMenuPowerActions":true,"ClearActivityOnExit":true,"RequireOwnerPinToLeave":false}',strftime('%Y-%m-%dT%H:%M:%fZ','now'));
