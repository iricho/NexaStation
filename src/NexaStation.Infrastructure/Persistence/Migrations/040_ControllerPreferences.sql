CREATE TABLE ControllerPreferences
(
    DeviceMatchKey TEXT PRIMARY KEY,
    PreferredGlyphSetId TEXT NULL,
    ConfirmConvention INTEGER NOT NULL,
    Deadzone REAL NOT NULL,
    RepeatDelayMilliseconds INTEGER NOT NULL,
    RepeatRateMilliseconds INTEGER NOT NULL,
    AnalogNavigationEnabled INTEGER NOT NULL DEFAULT 1,
    DpadNavigationEnabled INTEGER NOT NULL DEFAULT 1,
    WrapNavigationEnabled INTEGER NOT NULL DEFAULT 0,
    UpdatedAtUtc TEXT NOT NULL
);
