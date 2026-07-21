CREATE TABLE ProviderCredentialReferences(
 ProviderId TEXT PRIMARY KEY, CredentialStoreReference TEXT NOT NULL, DisplayAccountName TEXT NULL,
 ValidationState INTEGER NOT NULL, LastValidatedAtUtc TEXT NULL, UpdatedAtUtc TEXT NOT NULL);
