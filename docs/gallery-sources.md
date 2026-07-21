# Gallery sources

A Gallery source is a user-approved folder plus explicit options: source kind, include subfolders, images/videos, hidden files, watching, privacy, Home visibility, slideshow/background eligibility, and folder-album behavior.

The add flow previews exact, parent, and child overlaps before saving. Exact duplicates are rejected. Intentional parent/child sources remain possible after a warning, but duplicate work should be avoided. Source paths are normalized for identity while the display path is retained.

Removing a source deletes its NexaStation index, thumbnail records, and album memberships through database cascades. It never deletes, moves, renames, or edits source media. A missing or moved folder is retained as unavailable. **Locate Selected Source** changes the root while preserving media IDs, favorites, and album memberships where relative paths still match.

Suggested Windows folders are not auto-approved. Add them only through the folder picker and privacy review.
