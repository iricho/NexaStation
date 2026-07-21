CREATE TABLE DesktopControlProfiles(
 Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Enabled INTEGER NOT NULL DEFAULT 1,
 CursorSensitivity REAL NOT NULL, CursorAcceleration REAL NOT NULL, CursorDeadzone REAL NOT NULL, ScrollSensitivity REAL NOT NULL,
 LeftClickAction INTEGER NOT NULL, RightClickAction INTEGER NOT NULL, MiddleClickAction INTEGER NULL,
 ScrollAction INTEGER NOT NULL, BackAction INTEGER NULL, ForwardAction INTEGER NULL, EscapeAction INTEGER NULL,
 ToggleAction INTEGER NOT NULL, EmergencyDisableAction INTEGER NOT NULL, PauseOnPhysicalMouseInput INTEGER NOT NULL DEFAULT 1,
 UserManaged INTEGER NOT NULL DEFAULT 1, CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL);
CREATE TABLE LibraryItemDesktopControlProfiles(
 LibraryItemId TEXT PRIMARY KEY, DesktopControlProfileId TEXT NOT NULL, EnableOnLaunch INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE,
 FOREIGN KEY(DesktopControlProfileId) REFERENCES DesktopControlProfiles(Id) ON DELETE CASCADE);

INSERT OR IGNORE INTO DesktopControlProfiles(Id,Name,Enabled,CursorSensitivity,CursorAcceleration,CursorDeadzone,ScrollSensitivity,LeftClickAction,RightClickAction,MiddleClickAction,ScrollAction,BackAction,ForwardAction,EscapeAction,ToggleAction,EmergencyDisableAction,PauseOnPhysicalMouseInput,UserManaged,CreatedAtUtc,UpdatedAtUtc) VALUES
 ('basic-mouse','Basic Mouse',1,900,1.7,0.18,480,1,2,NULL,4,5,NULL,5,16,17,1,0,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('media-application','Media Application',1,700,1.4,0.20,420,12,2,NULL,4,5,NULL,5,16,17,1,0,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 ('browser','Browser',1,900,1.7,0.18,520,1,2,NULL,4,10,11,5,16,17,1,0,strftime('%Y-%m-%dT%H:%M:%fZ','now'),strftime('%Y-%m-%dT%H:%M:%fZ','now'));
