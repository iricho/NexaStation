CREATE TABLE CaptureSettings(
 Id INTEGER PRIMARY KEY CHECK(Id=1), ScreenshotFolder TEXT NULL, OutputFormat INTEGER NOT NULL DEFAULT 0,
 JpegQuality INTEGER NOT NULL DEFAULT 92, IncludeCursor INTEGER NOT NULL DEFAULT 0,
 AutomaticallyAddToGallery INTEGER NOT NULL DEFAULT 1, ShowNotification INTEGER NOT NULL DEFAULT 1,
 IncludeSanitizedTitle INTEGER NOT NULL DEFAULT 0, GlobalHotkeyEnabled INTEGER NOT NULL DEFAULT 0,
 GlobalHotkey TEXT NULL, UpdatedAtUtc TEXT NOT NULL);
INSERT INTO CaptureSettings(Id,UpdatedAtUtc) VALUES(1,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
