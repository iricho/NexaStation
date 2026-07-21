ALTER TABLE GallerySources ADD COLUMN ShowOnHome INTEGER NOT NULL DEFAULT 1;
ALTER TABLE GallerySources ADD COLUMN ShowFilenames INTEGER NOT NULL DEFAULT 1;
ALTER TABLE GallerySources ADD COLUMN AllowSlideshow INTEGER NOT NULL DEFAULT 1;
ALTER TABLE GallerySources ADD COLUMN AllowBackgroundUse INTEGER NOT NULL DEFAULT 1;
ALTER TABLE GallerySources ADD COLUMN FolderAlbumsEnabled INTEGER NOT NULL DEFAULT 0;
ALTER TABLE GallerySources ADD COLUMN Available INTEGER NOT NULL DEFAULT 1;
ALTER TABLE GallerySources ADD COLUMN PreferredApplicationId TEXT NULL;
ALTER TABLE GalleryMediaItems ADD COLUMN PreferredApplicationId TEXT NULL;
CREATE TABLE GalleryApplicationState(
 Id INTEGER PRIMARY KEY CHECK(Id=1), CurrentGalleryView INTEGER NOT NULL, SelectedGalleryItemId TEXT NULL,
 SelectedGallerySourceId TEXT NULL, SelectedAlbumId TEXT NULL, GalleryGridSize INTEGER NOT NULL,
 GallerySortMode INTEGER NOT NULL, GalleryFilter TEXT NULL, ViewerZoomMode INTEGER NOT NULL,
 SlideshowInterval INTEGER NOT NULL, ScrollPosition REAL NOT NULL DEFAULT 0, UpdatedAtUtc TEXT NOT NULL);
CREATE TABLE GalleryLaunchPreferences(
 Id TEXT PRIMARY KEY, Scope INTEGER NOT NULL, ScopeId TEXT NULL, ApplicationLibraryItemId TEXT NOT NULL,
 ArgumentsTemplate TEXT NOT NULL, HideFrontendWhileRunning INTEGER NOT NULL DEFAULT 0,
 RestoreFrontendAfterExit INTEGER NOT NULL DEFAULT 0, TrackingMode INTEGER NOT NULL DEFAULT 0,
 UpdatedAtUtc TEXT NOT NULL, UNIQUE(Scope,ScopeId));
CREATE TABLE GalleryBackgroundState(
 Id INTEGER PRIMARY KEY CHECK(Id=1), MediaItemId TEXT NOT NULL, ManagedPath TEXT NOT NULL,
 DisplayMode INTEGER NOT NULL, UpdatedAtUtc TEXT NOT NULL);
