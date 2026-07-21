# Smart collections

Smart collections use structured field/operator/value rules and Match All or Match Any semantics. Supported fields include content type, provider, platform, favorites, hidden/availability state, recent activity, dates, playtime, category, emulator, title, and user tag. No arbitrary SQL, expressions, scripts, or executable content is accepted.

Evaluation requests at most 5,000 candidates and returns at most 500 results; dashboard rows impose a smaller limit. Hidden content is excluded unless the collection explicitly defines a hidden-state rule. Results are cached for 15 seconds and invalidated after relevant collection changes.

Rules are validated before evaluation. Unknown fields/operators, missing required values, missing collections, and invalid sources fail locally without breaking dashboard startup.
