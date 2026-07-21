CREATE TABLE DisplayPreferences
(
    Id TEXT PRIMARY KEY CHECK(Id='local'),
    PreferredMode INTEGER NOT NULL DEFAULT 0,
    PreferredDisplayId TEXT NULL,
    StartOnPreferredDisplay INTEGER NOT NULL DEFAULT 0,
    FullscreenMode INTEGER NOT NULL DEFAULT 1,
    InterfaceScale REAL NOT NULL DEFAULT 1.0,
    ReduceEffectsOnBattery INTEGER NOT NULL DEFAULT 0,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE TABLE DisplayCalibrations
(
    DisplayId TEXT PRIMARY KEY,
    LeftInset INTEGER NOT NULL DEFAULT 0,
    TopInset INTEGER NOT NULL DEFAULT 0,
    RightInset INTEGER NOT NULL DEFAULT 0,
    BottomInset INTEGER NOT NULL DEFAULT 0,
    CalibrationWidth INTEGER NOT NULL,
    CalibrationHeight INTEGER NOT NULL,
    CalibrationScale REAL NOT NULL,
    ArtworkOverlayDarkness REAL NOT NULL DEFAULT 0.35,
    UpdatedAtUtc TEXT NOT NULL
);
INSERT INTO DisplayPreferences(Id,PreferredMode,PreferredDisplayId,StartOnPreferredDisplay,FullscreenMode,InterfaceScale,ReduceEffectsOnBattery,UpdatedAtUtc)
VALUES('local',0,NULL,0,1,1.0,0,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
