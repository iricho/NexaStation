CREATE TABLE InterfaceConfigurationExports
(
    Id TEXT PRIMARY KEY,
    FormatVersion INTEGER NOT NULL,
    ConfigurationJson TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    Purpose TEXT NOT NULL
);
