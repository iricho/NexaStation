CREATE TABLE GameExternalIdentities(
 Id TEXT PRIMARY KEY, LibraryItemId TEXT NOT NULL, ProviderId TEXT NOT NULL, ExternalGameId TEXT NOT NULL,
 IdentityType INTEGER NOT NULL, Confidence INTEGER NOT NULL, ConfirmedByUser INTEGER NOT NULL DEFAULT 0,
 CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL,
 FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE,
 UNIQUE(ProviderId,ExternalGameId,LibraryItemId));
CREATE INDEX IX_GameExternalIdentities_Item ON GameExternalIdentities(LibraryItemId);
CREATE INDEX IX_GameExternalIdentities_ProviderGame ON GameExternalIdentities(ProviderId,ExternalGameId);
