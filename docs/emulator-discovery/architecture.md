# Emulator discovery architecture

`IEmulatorDiscoveryService` coordinates bounded discovery sources, definition matching, validation, version metadata, duplicate detection, and preview results. Settings supplies only explicit options and approved roots; it does not scan paths itself. Adding a candidate is a separate confirmed action through the existing emulation library.
