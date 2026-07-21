# Public-alpha validation matrix

Automated CI covers compilation, existing behavior, onboarding state, startup/crash policy, database validation, backup/restore/tamper rejection, redaction, version/channel policy, activation routing, and release metadata. Manual Windows validation remains required for:

- clean VM install and first launch;
- packaged StartupTask and protocol activation;
- real controller navigation and focus on every onboarding/recovery action;
- Steam refresh and a safe game launch on an owned test title;
- emulator launch on a configured test ROM the tester is authorized to use;
- Windows application scan and a safe application launch;
- gallery indexing/viewer with tester-owned files;
- Modern, Crossbar, and Handheld layouts at supported resolutions;
- Quick Menu, media, audio, desktop control, and system confirmation flows;
- signed MSIX install/update/uninstall/reinstall and appinstaller behavior;
- low disk, unwritable directory, damaged backup, corrupt DB, failed migration, crash loop, offline update, and invalid signature;
- startup/navigation/memory measurements on the same hardware as the baseline.

Record OS build, package hash, app version, hardware, controller, data disposition, and evidence for each run.
