# Recovery

Every startup records health stages. Three failures within ten minutes request safe mode; a healthy dashboard clears the crash-loop history. Safe mode disables optional startup refreshes and gallery watchers while keeping the local dashboard and recovery actions available.

Before opening the normal dashboard, NexaStation checks SQLite integrity and migration history. Existing databases receive a validated online `.nxbackup` snapshot before migration. Restore rejects unknown entries, traversal paths, unsupported manifests, size/hash mismatches, and databases that fail SQLite validation. The current database is backed up before replacement.

Recovery actions include retry, safe-mode restart, validated backup, validated restore, redacted diagnostics export, and a two-step reset. Reset backs up and preserves the previous database before a new empty library is created. No recovery flow deletes original external content.

Crash reports are local JSON under `CrashReports`; diagnostics ZIPs exclude the database and original media. If disk permissions prevent state/backup creation, preserve the data directory and resolve the Windows permission or disk-space problem before retrying.
