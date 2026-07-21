# Benchmark methodology

`benchmarks/NexaStation.Benchmarks` uses deterministic synthetic items only. Run Release builds, warm each scenario once, collect at least seven iterations, and compare medians on the same machine/profile. Use 25 iterations for release evidence. Record tool/runtime version and variance; do not treat shared CI timing as a release gate.

The latest local Phase 9 quick-run evidence is recorded in [phase-9-results.md](phase-9-results.md). It is intentionally separated from hardware certification and soak-test evidence.
