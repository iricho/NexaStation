# Updates

Development builds do not contact an update endpoint by default. Alpha update checks require the Alpha channel plus an explicit enabled preference. The update service compares semantic versions, validates channel match, and returns a user-visible result; it does not silently install a package.

Release automation produces an `.appinstaller` only after version, license, notices, assets, signing identity, and package generation pass. The template under `eng/` contains placeholders and is not a distributable installer.

Before updating, use the in-app validated backup action. Database migrations are forward-only and backed up automatically; older builds may refuse a newer schema rather than modifying it.
