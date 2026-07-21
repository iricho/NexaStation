CREATE TABLE EmulatorPlatforms(
 Id TEXT PRIMARY KEY, Name TEXT NOT NULL, ShortName TEXT NOT NULL, Manufacturer TEXT NULL, Generation INTEGER NULL,
 ExtensionsJson TEXT NOT NULL, DefaultAdapterIdsJson TEXT NULL, IconAsset TEXT NULL, Enabled INTEGER NOT NULL DEFAULT 1,
 CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL);

CREATE TABLE GameSources(
 Id TEXT PRIMARY KEY, Name TEXT NOT NULL, FolderPath TEXT NOT NULL, NormalizedFolderPath TEXT NOT NULL,
 PlatformId TEXT NOT NULL, IncludeSubfolders INTEGER NOT NULL DEFAULT 1, Enabled INTEGER NOT NULL DEFAULT 1,
 FollowSymbolicLinks INTEGER NOT NULL DEFAULT 0, ScanHiddenFiles INTEGER NOT NULL DEFAULT 0,
 AdapterPreferenceId TEXT NULL, CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL, LastSuccessfulScanAtUtc TEXT NULL,
 FOREIGN KEY(PlatformId) REFERENCES EmulatorPlatforms(Id),
 FOREIGN KEY(AdapterPreferenceId) REFERENCES EmulatorInstallations(Id) ON DELETE SET NULL,
 UNIQUE(PlatformId, NormalizedFolderPath));

INSERT OR IGNORE INTO Providers(Id,DisplayName,Enabled,CreatedAtUtc,UpdatedAtUtc) VALUES('emulation','Emulation',1,datetime('now'),datetime('now'));

INSERT OR IGNORE INTO EmulatorPlatforms VALUES
('gamecube','GameCube','GC','Nintendo',6,'[".iso",".gcm",".ciso",".gcz",".wia",".rvz",".dol",".elf"]','["dolphin"]',NULL,1,datetime('now'),datetime('now')),
('wii','Wii','Wii','Nintendo',7,'[".iso",".wbfs",".ciso",".gcz",".wia",".rvz",".dol",".elf"]','["dolphin"]',NULL,1,datetime('now'),datetime('now')),
('playstation-2','PlayStation 2','PS2','Sony',6,'[".iso",".bin",".img",".mdf",".chd",".elf"]','["pcsx2"]',NULL,1,datetime('now'),datetime('now')),
('playstation-portable','PlayStation Portable','PSP','Sony',7,'[".iso",".cso",".chd",".pbp",".elf"]','["ppsspp"]',NULL,1,datetime('now'),datetime('now')),
('nes','Nintendo Entertainment System','NES','Nintendo',3,'[".nes"]','["retroarch"]',NULL,1,datetime('now'),datetime('now')),
('snes','Super Nintendo Entertainment System','SNES','Nintendo',4,'[".sfc",".smc"]','["retroarch"]',NULL,1,datetime('now'),datetime('now')),
('game-boy','Game Boy','GB','Nintendo',4,'[".gb"]','["retroarch"]',NULL,1,datetime('now'),datetime('now')),
('game-boy-color','Game Boy Color','GBC','Nintendo',5,'[".gbc"]','["retroarch"]',NULL,1,datetime('now'),datetime('now')),
('game-boy-advance','Game Boy Advance','GBA','Nintendo',6,'[".gba"]','["retroarch"]',NULL,1,datetime('now'),datetime('now')),
('nintendo-64','Nintendo 64','N64','Nintendo',5,'[".n64",".z64",".v64"]','["retroarch"]',NULL,1,datetime('now'),datetime('now')),
('sega-genesis','Sega Genesis / Mega Drive','Genesis','Sega',4,'[".gen",".md"]','["retroarch"]',NULL,1,datetime('now'),datetime('now')),
('sega-master-system','Sega Master System','SMS','Sega',3,'[".sms"]','["retroarch"]',NULL,1,datetime('now'),datetime('now')),
('playstation','PlayStation','PS1','Sony',5,'[".bin",".cue",".chd",".pbp"]','["retroarch"]',NULL,1,datetime('now'),datetime('now')),
('arcade-generic','Arcade (Generic)','Arcade',NULL,NULL,'[]','["retroarch","custom"]',NULL,1,datetime('now'),datetime('now')),
('playstation-3-experimental','PlayStation 3 (Experimental)','PS3','Sony',7,'[".elf",".self"]','["rpcs3"]',NULL,1,datetime('now'),datetime('now')),
('custom','Custom Platform','Custom',NULL,NULL,'[]','["custom"]',NULL,1,datetime('now'),datetime('now'));
