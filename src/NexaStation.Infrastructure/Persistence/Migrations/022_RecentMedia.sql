CREATE TABLE RecentMedia
(
    Id TEXT PRIMARY KEY,
    GalleryMediaItemId TEXT NULL,
    MediaType INTEGER NOT NULL,
    OpenedAtUtc TEXT NOT NULL,
    PlayerLibraryItemId TEXT NULL,
    LaunchMethod INTEGER NOT NULL,
    TrackingConfidence INTEGER NOT NULL,
    FOREIGN KEY (GalleryMediaItemId) REFERENCES GalleryMediaItems(Id) ON DELETE SET NULL,
    FOREIGN KEY (PlayerLibraryItemId) REFERENCES LibraryItems(Id) ON DELETE SET NULL
);
CREATE INDEX IX_RecentMedia_OpenedAtUtc ON RecentMedia(OpenedAtUtc DESC);
