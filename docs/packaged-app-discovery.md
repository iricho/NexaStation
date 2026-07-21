# Packaged application discovery

The packaged-app reader uses the current-user Windows package application-list APIs and each package's public app-list entries. It records display name, package family/full name, application ID, publisher, shell identity, and AppUserModelId (AUMID) when available. It does not inspect another user's packages or private package data.

An AUMID is the stable Windows activation identity in the form `package-family!application`. AUMID targets launch through `IApplicationActivationManager`; Shell identities are retained as a fallback. The activation API may return a process hint, but packaged applications can reuse brokered or pre-existing processes. NexaStation therefore treats packaged-process tracking as best-effort and records no duration when a reliable identity cannot be established.

Package discovery is read-only. NexaStation does not install, register, update, reset, repair, or uninstall packages.
