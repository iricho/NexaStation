CREATE TABLE AchievementRecords(
 ProviderId TEXT NOT NULL, ProviderAchievementId TEXT NOT NULL, LibraryItemId TEXT NOT NULL,
 Name TEXT NOT NULL, Description TEXT NULL, IconPath TEXT NULL, LockedIconPath TEXT NULL,
 Unlocked INTEGER NOT NULL DEFAULT 0, UnlockedAtUtc TEXT NULL, Hidden INTEGER NOT NULL DEFAULT 0,
 ProgressCurrent REAL NULL, ProgressMaximum REAL NULL, Provenance TEXT NOT NULL,
 RetrievedAtUtc TEXT NOT NULL, ImportedAtUtc TEXT NOT NULL, ActivityRecorded INTEGER NOT NULL DEFAULT 0,
 PRIMARY KEY(ProviderId,ProviderAchievementId,LibraryItemId),
 FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE);
