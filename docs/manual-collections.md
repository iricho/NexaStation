# Manual collections

Manual collections contain ordered typed references, including library and Gallery targets and nested collections. Creating, renaming, adding, removing, and reordering updates only NexaStation collection records; referenced games, applications, media, albums, and sources remain untouched.

Collections can be nested to five levels. Self-reference and recursive item graphs are rejected before persistence. Folder collections provide hierarchy, manual collections can be pinned, and either manual or smart collections can become a bounded dashboard row.

Mixed target types retain their type discriminator so an ID from one subsystem cannot be mistaken for another.
