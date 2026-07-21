CREATE TABLE DashboardConfigurations
(
    Id TEXT PRIMARY KEY,
    LayoutId TEXT NOT NULL,
    Name TEXT NOT NULL,
    IsDefault INTEGER NOT NULL DEFAULT 0,
    HeroEnabled INTEGER NOT NULL DEFAULT 1,
    CardDensity INTEGER NOT NULL,
    RowSpacing INTEGER NOT NULL,
    ShowStatusBar INTEGER NOT NULL DEFAULT 1,
    ShowFooterHints INTEGER NOT NULL DEFAULT 1,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    UNIQUE(LayoutId, Name)
);
