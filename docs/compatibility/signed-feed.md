# Signed compatibility feed

The feed foundation accepts only HTTPS, schema version 1, a SHA-256 payload digest, and an RSA-SHA256 signature from a pinned key. It validates the entire JSON tree and every typed profile before replacing the previous imported set. Failure preserves the previous known-good data.

The feature is disabled in current builds because no owner-approved feed URL and signing key exist. Update checks do not silently enable it.
