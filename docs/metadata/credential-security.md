# Credential security

Provider keys are stored as generic credentials in Windows Credential Manager. SQLite stores only an opaque target reference, account label, validation state, and timestamp. Secrets must never appear in logs, diagnostics, settings, exports, URLs displayed by the UI, or test fixtures. Removing an account deletes both the protected credential and its reference.
