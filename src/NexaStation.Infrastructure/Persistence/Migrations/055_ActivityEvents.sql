CREATE TABLE ActivityEvents(
 Id TEXT PRIMARY KEY, EventType INTEGER NOT NULL, TargetType INTEGER NOT NULL, TargetId TEXT NULL,
 SessionId TEXT NULL, TitleSnapshot TEXT NULL, SubtitleSnapshot TEXT NULL, ProviderId TEXT NULL,
 PlatformId TEXT NULL, OccurredAtUtc TEXT NOT NULL, EndedAtUtc TEXT NULL, DurationSeconds INTEGER NULL,
 Result INTEGER NOT NULL, PrivacyLevel INTEGER NOT NULL, GuestSessionId TEXT NULL, MetadataJson TEXT NULL,
 CorrelationId TEXT NULL, CreatedAtUtc TEXT NOT NULL);
CREATE INDEX IX_ActivityEvents_OccurredAtUtc ON ActivityEvents(OccurredAtUtc DESC);
CREATE INDEX IX_ActivityEvents_EventType ON ActivityEvents(EventType);
CREATE INDEX IX_ActivityEvents_TargetId ON ActivityEvents(TargetId);
CREATE INDEX IX_ActivityEvents_SessionId ON ActivityEvents(SessionId);
CREATE INDEX IX_ActivityEvents_PrivacyLevel ON ActivityEvents(PrivacyLevel);
CREATE INDEX IX_ActivityEvents_GuestSessionId ON ActivityEvents(GuestSessionId);
CREATE UNIQUE INDEX UX_ActivityEvents_SessionEvent ON ActivityEvents(SessionId,EventType) WHERE SessionId IS NOT NULL;
