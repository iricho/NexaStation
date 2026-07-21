# Network status

Network status uses `Windows.Networking.Connectivity.NetworkInformation`, subscribes to status changes, and reads the current profile after a notification. Internet access is displayed as a Windows-provided hint, not as a guarantee.

The Quick Menu may show connection availability, profile name, Wi-Fi/Ethernet classification, metered state, and roaming state when safely available. It never performs an external probe, pings a server, displays IP addresses, exposes saved Wi-Fi data, or changes network configuration.

An unavailable API or absent profile produces a readable offline/unknown state without affecting local library operation.
