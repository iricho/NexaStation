# Metadata and artwork architecture

Phase 11 separates identity, provider search, deterministic scoring, download validation, managed storage, assignment, and presentation. Local identity is resolved before provider calls. Local files are tried first; remote providers run only after the user enables online artwork. Remote responses never bind directly to XAML.

Artwork is copied into the NexaStation-managed `Data\Artwork` store under opaque content hashes. `ArtworkAssignments` records the current asset and bounded history. Provider candidates and durable job state remain in SQLite, while credentials remain in Windows Credential Manager.
