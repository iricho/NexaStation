CREATE TABLE DashboardRows
(
    Id TEXT PRIMARY KEY,
    DashboardConfigurationId TEXT NOT NULL,
    RowKey TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    RowType INTEGER NOT NULL,
    SourceDefinitionJson TEXT NULL,
    SortOrder INTEGER NOT NULL,
    Enabled INTEGER NOT NULL DEFAULT 1,
    MaximumItems INTEGER NOT NULL DEFAULT 12,
    CardStyle INTEGER NOT NULL,
    CardSize INTEGER NOT NULL,
    ShowTitle INTEGER NOT NULL DEFAULT 1,
    ShowProviderBadge INTEGER NOT NULL DEFAULT 0,
    ShowProgress INTEGER NOT NULL DEFAULT 1,
    ShowEmptyState INTEGER NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (DashboardConfigurationId) REFERENCES DashboardConfigurations(Id) ON DELETE CASCADE,
    UNIQUE(DashboardConfigurationId, RowKey)
);
