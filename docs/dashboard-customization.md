# Dashboard customization

Dashboard configurations are stored per layout. Each row has a stable key, source type/reference, order, enabled state, maximum item count, card style/size, title and metadata flags. The Appearance dialog opens a controller-accessible row editor for enabling, sizing, styling, and reordering rows.

`IDashboardConfigurationService` clamps item limits to 1–100, normalizes persisted ordering, and rejects configurations where every row is disabled. Reset affects only the selected layout's dashboard rows and retains library/provider/Gallery data.

Collection rows retain stable collection IDs. Deleted or invalid sources resolve safely. All row projections and smart queries are bounded, and only the active layout creates a full visible tree.
