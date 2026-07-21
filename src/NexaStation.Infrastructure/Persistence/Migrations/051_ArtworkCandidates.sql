CREATE TABLE ArtworkCandidates(
 Id TEXT PRIMARY KEY, ArtworkJobItemId TEXT NOT NULL, ProviderId TEXT NOT NULL, ProviderGameId TEXT NOT NULL,
 ProviderAssetId TEXT NULL, AssetType INTEGER NULL, Title TEXT NOT NULL, PlatformId TEXT NULL, Region TEXT NULL,
 Score INTEGER NOT NULL, Decision INTEGER NOT NULL, ReasonsJson TEXT NOT NULL, ConflictsJson TEXT NOT NULL,
 ThumbnailUrlHash TEXT NULL, MetadataJson TEXT NULL, ExpiresAtUtc TEXT NULL, CreatedAtUtc TEXT NOT NULL,
 FOREIGN KEY(ArtworkJobItemId) REFERENCES ArtworkJobItems(Id) ON DELETE CASCADE);
CREATE INDEX IX_ArtworkCandidates_JobItem ON ArtworkCandidates(ArtworkJobItemId,Score DESC);
