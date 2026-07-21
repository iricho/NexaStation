# Pinning

Pins are lightweight references to library items, Gallery entities, collections, system shortcuts, or settings pages. A pin records its target type/ID, location, optional group, display name, order, availability, and UTC timestamps; it does not copy or own target data.

Duplicate target/location pairs are rejected while the same target may be pinned in different locations. Reordering must contain every pin in that location exactly once. Removing a pin never removes the target. Unavailable pins remain representable so disconnected content does not silently lose user intent.
