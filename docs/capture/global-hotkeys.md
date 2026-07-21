# Global screenshot hotkey

The global hotkey is disabled by default and requires explicit opt-in. When enabled outside Safe Mode, NexaStation registers one `RegisterHotKey` combination and reports conflicts. It uses no keyboard hook, records no arbitrary input, does not poll, and unregisters during shutdown. Pressing it is a direct capture action; enabling it is not consent for automatic capture.
