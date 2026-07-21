# Provider management

Provider actions are based on persisted structured identities. Steam navigation accepts only a `steam:` URI already associated with the selected target. Other application management falls back to `ms-settings:appsfeatures`. No registry uninstall string, manifest command, executable path, or web-supplied command is executed.

Provider scans remain read-only and authoritative only for their own source metadata. Removing a NexaStation entry does not modify the provider library and does not suppress later rediscovery.
