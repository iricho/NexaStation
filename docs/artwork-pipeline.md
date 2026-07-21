# Artwork pipeline

Single-item artwork actions are exposed through the shared context menu. Find Artwork uses the existing preflight/job pipeline, Choose Local Cover uses the existing editor and provider-override rules, and Reset/Undo uses managed assignment history. These actions never edit or delete original user artwork.

Resolution precedence is: user-selected/locked assignment, current managed provider assignment, existing imported asset, safe sidecar, then generated fallback. Search follows identity resolution → provider status/privacy gate → structured candidates → deterministic scoring → auto-apply or review → static asset selection → streamed validation → content-addressed managed copy → atomic assignment/provenance write.

PNG, JPEG, and WebP signatures are checked against MIME, declared/actual size, completeness markers, and bounded dimensions. Remote URLs must stay HTTPS through a limited redirect chain. Temporary files are removed on failure. The source URL is retained only as a SHA-256 hash in the assignment. History retains the current asset and two ordinary prior assets unless pinned.
