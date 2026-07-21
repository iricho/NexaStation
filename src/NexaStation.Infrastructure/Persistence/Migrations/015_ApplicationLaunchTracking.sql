CREATE TABLE ApplicationLaunchTracking(
 LibraryItemId TEXT PRIMARY KEY, TrackingMode INTEGER NOT NULL, ExpectedProcessName TEXT NULL,
 ExpectedExecutablePath TEXT NULL, ExpectedAppUserModelId TEXT NULL, ExpectedWindowClass TEXT NULL,
 DetectionTimeoutSeconds INTEGER NOT NULL, ExitGracePeriodSeconds INTEGER NOT NULL, UserManaged INTEGER NOT NULL DEFAULT 0,
 UpdatedAtUtc TEXT NOT NULL, FOREIGN KEY(LibraryItemId) REFERENCES LibraryItems(Id) ON DELETE CASCADE);

ALTER TABLE LaunchTargets ADD COLUMN TrackingMode INTEGER NOT NULL DEFAULT 1;
ALTER TABLE LaunchTargets ADD COLUMN ExpectedExecutablePath TEXT NULL;
ALTER TABLE LaunchTargets ADD COLUMN ExpectedWindowClass TEXT NULL;
ALTER TABLE LaunchTargets ADD COLUMN DetectionTimeoutSeconds INTEGER NOT NULL DEFAULT 30;
ALTER TABLE LaunchTargets ADD COLUMN ExitGracePeriodSeconds INTEGER NOT NULL DEFAULT 3;
ALTER TABLE PlaySessions ADD COLUMN SessionKind INTEGER NOT NULL DEFAULT 0;
ALTER TABLE PlaySessions ADD COLUMN TrackingMode INTEGER NOT NULL DEFAULT 1;
ALTER TABLE PlaySessions ADD COLUMN ForegroundProcessId INTEGER NULL;
ALTER TABLE PlaySessions ADD COLUMN ForegroundWindowIdentity TEXT NULL;
