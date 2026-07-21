# Release process

1. Obtain and record the owner's project-license decision; add the exact approved root `LICENSE` text.
2. Review `THIRD-PARTY-NOTICES.md` against the restored dependency graph and complete trademark/asset review.
3. Set one version in `Version.props`; match `Package.appxmanifest`, tag `vX.Y.Z`, changelog, and appinstaller package version.
4. Run Release x64 restore, build, all tests, publish, release-readiness validation, and unsigned package generation.
5. Run clean-VM install, first launch, onboarding resume, all three layouts, provider/gallery smoke, restart, update, uninstall/reinstall, and data-retention checks.
6. Exercise corrupt DB, failed migration, backup restore, reset, crash loop, safe mode, unwritable folder, low-disk, offline update, and invalid package/signature scenarios.
7. Measure startup, navigation, and cold-Home memory against the recorded baseline. Document hardware and results.
8. Sign only in protected release automation. Never commit a certificate or password. Verify signature, publisher, SmartScreen/reputation expectations, and hashes before upload.
9. Replace appinstaller URL placeholders with approved HTTPS release URLs, publish immutable artifacts, then perform an installed update test.

Any failed or unperformed gate must be reported as a blocker, not assumed complete.
