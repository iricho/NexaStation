# External file protection

Item actions must not delete installations, ROMs, emulators, saves, Gallery source media, launcher manifests, or user artwork. Library, Gallery, and collection removal methods execute database statements only. Cache services may remove NexaStation-managed thumbnails, background copies, temporary downloads, and bounded managed artwork history.

Local navigation requires an existing full filesystem path and opens Explorer without a shell command line. Provider navigation is allowlisted to structured provider or Windows Settings URIs. User-provided commands, uninstall strings, scripts, shell expansion, elevation, registry cleanup, and third-party uninstall tools are forbidden.
