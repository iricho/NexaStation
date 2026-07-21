CREATE TABLE TextInputSettings
(
    Id TEXT PRIMARY KEY CHECK(Id='local'),
    PreferredLayoutId TEXT NOT NULL DEFAULT 'en-qwerty',
    SuggestionsEnabled INTEGER NOT NULL DEFAULT 1,
    SearchHistoryEnabled INTEGER NOT NULL DEFAULT 0,
    MaximumSuggestions INTEGER NOT NULL DEFAULT 8,
    UpdatedAtUtc TEXT NOT NULL
);
INSERT INTO TextInputSettings(Id,PreferredLayoutId,SuggestionsEnabled,SearchHistoryEnabled,MaximumSuggestions,UpdatedAtUtc)
VALUES('local','en-qwerty',1,0,8,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
