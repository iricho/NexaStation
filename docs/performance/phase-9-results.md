# Phase 9 local benchmark evidence

Recorded 2026-07-19 using the Release build and the quick, advisory benchmark profile. The harness used generated synthetic records only and classified the host as an unclassified local development machine; these numbers are not a low-end hardware certification.

| Scenario | Dataset | Iterations | Median | Range |
| --- | ---: | ---: | ---: | ---: |
| Library record creation | 1,000 | 7 | 0.1820 ms | 0.1807–0.1968 ms |
| Library record creation | 5,000 | 7 | 0.7701 ms | 0.7633–0.7996 ms |
| Gallery query/projection | 10,000 | 7 | 1.6224 ms | 1.2040–2.6304 ms |
| Smart-collection evaluation | 5,000 | 7 | 1.1981 ms | 1.1881–1.2145 ms |

The results prove repeatability of the synthetic algorithm-level harness and exercise the required 5,000-item library and 10,000-item Gallery datasets. They do not measure UI realization, disk-backed provider scans, GPU use, steady-state memory, four-hour idle behavior, or representative low-end hardware. Those remain release-blocker evidence gates in the Beta readiness report.
