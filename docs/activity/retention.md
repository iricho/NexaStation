# Activity retention

Defaults are 365 days, 20,000 events, completed game sessions retained, capture events retained, and artwork-change events not retained. Cleanup is at most weekly unless explicitly forced and deletes in bounded batches. It does not delete `PlaySessions`, aggregate playtime, library targets, or files. Event metadata is capped at 16 KiB.
