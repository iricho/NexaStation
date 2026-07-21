# Activity timeline

Phase 12 records local, UTC activity events for confirmed game/application launches and exits, user-created captures, completed external recordings discovered in approved folders, remote-play sessions, and selected provider/system outcomes. Start and end events share a session ID and duplicate completion is ignored. Queries are paginated, newest first, and can filter by event, target, provider, platform, or date.

The timeline is local SQLite data. Deleting an event never deletes a library item, Gallery file, screenshot, recording, provider achievement, or aggregate playtime.
