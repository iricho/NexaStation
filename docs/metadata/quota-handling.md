# Quotas and rate limits

All remote calls use the shared provider coordinator: two concurrent calls per provider and at least 350 ms between starts. `Retry-After` and authentication cooldowns are honored. Jobs preserve pending work when offline or limited. NexaStation does not rotate accounts, bypass quotas, or infer a reset time that the provider did not supply.
