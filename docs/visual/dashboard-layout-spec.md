# Dashboard layout specification

The window has four rows: navigation, notification overlay, scrollable content, and footer. Artwork and its readability scrim span the complete root grid. The dashboard content has no fixed maximum width.

| State | Minimum viewport | Safe horizontal margin | Hero height | Paired sections |
|---|---:|---:|---:|---|
| Compact720 | 0 | 28–30 | 218 | Stack vertically |
| Standard1080 | 1500 | 48–56 | 286 | Two columns |
| Large1440 | 2200 | 68–72 | 316 | Two/three intentional columns |

Horizontal card rows use `ItemsStackPanel` virtualization and hidden scrollbars. Cards keep bounded widths so ultrawide layouts reveal more items rather than stretching tiles. Additional configured rows are below the fold. The footer occupies its own grid row and cannot overlap scrollable content.

Compact behavior collapses the brand subtitle, reduces safe margins, stacks Apps and Recent Photos, and stacks the three lower groups. Essential navigation remains labeled and horizontally scrollable rather than being reduced to tiny icon-only buttons.
