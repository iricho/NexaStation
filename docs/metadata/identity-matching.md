# Game identity matching

Identities can contain provider IDs, Steam App IDs, emulator platform, normalized title, disc serial, region, revision, disc number, and optional small-ROM hashes. Provider ID, serial, or hash evidence is authoritative. Platform conflict, DLC/base-game conflict, serial conflict, and original/remaster conflict reject a match. Filename-only title evidence is never sufficient for unattended remote auto-apply.
