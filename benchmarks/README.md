# NexaStation benchmarks

The benchmark executable uses deterministic synthetic library and Gallery records. It never reads a personal library, Gallery source, crash report, or diagnostics bundle.

Run a quick advisory sample with:

```powershell
dotnet run --project benchmarks/NexaStation.Benchmarks -c Release
```

Use `-- --full` for 25 measured iterations. Public CI results are advisory because shared runners are noisy; release decisions require a recorded machine profile and comparison with the previous release on the same hardware.
