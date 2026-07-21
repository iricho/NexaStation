CREATE TABLE GalleryDefaultSourceState(
    Id INTEGER PRIMARY KEY CHECK(Id = 1),
    PicturesProposalState TEXT NOT NULL DEFAULT 'NotShown',
    PicturesSourceId TEXT NULL REFERENCES GallerySources(Id) ON DELETE SET NULL,
    NeverAskAgain INTEGER NOT NULL DEFAULT 0 CHECK(NeverAskAgain IN (0,1)),
    LastPromptedAtUtc TEXT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
