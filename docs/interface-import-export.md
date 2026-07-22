# Interface import and export

Export produces human-readable versioned JSON for layouts, dashboards and rows, pins, Shelves, Shelf items and rules, custom theme selection, sound settings, and visual performance. It intentionally excludes library/provider content, lifecycle statuses, private Gallery data, credentials, original media, executable files, and caches. Lifecycle statuses are library-specific data covered by a database backup.

Import parses into typed records, rejects unknown versions/enums, scripts, external URLs, and traversal, and presents counts/warnings before confirmation. A full current configuration backup is stored first. SQLite applies customization tables in one transaction before validated settings are activated.

Failed validation does not mutate state. Provider records and library content are outside the customization store and cannot be replaced by an interface import.
