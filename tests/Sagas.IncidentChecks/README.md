# Isolated incident diagnostics

This diagnostic project is development tooling and is not part of the mod package.
Never point a diagnostic at a running profile. First copy the **entire** Sagas data
directory, including `sagas.db`, `sagas-log.db` and outbox journals, into an ignored
`.dev/incidents/...` evidence directory while the game is closed. Do not copy the
plugin configuration or credentials.

The console checks only open a second, newly created copy of that evidence:

```powershell
dotnet run --project tests/Sagas.IncidentChecks/Sagas.IncidentChecks.csproj -- .dev/incidents/example/evidence .dev/incidents/example/run1
```

They enumerate collection counts, read player/event/map/chapter data, rewrite
existing player snapshots, replay pending outbox events, and checkpoint. Output
uses counts instead of player names or world identifiers. The evidence and
original profile remain untouched. Exceptions can contain file paths; inspect
reports before sharing them publicly.

`Sagas.MonoIncident.csproj` builds a library exposing
`ValheimSagas.Incident.MonoEntry.Run()`. An isolated Mono embedding harness can
invoke it with `SAGAS_INCIDENT_OUTPUT` set to a new directory. It creates only
explicitly synthetic player/map data, crosses repeated automatic checkpoint
thresholds, and writes an automatically flushed `report.txt`. This checks the
installed Unity Mono runtime without starting the game or opening saves; it is
not an in-game test.

To test recovery with the installed Unity runtime after building the Mono probe:

```powershell
dotnet build tests/Sagas.IncidentChecks/Sagas.MonoIncident.csproj -c Release
$env:SAGAS_INCIDENT_EVIDENCE = (Resolve-Path '.dev/incidents/example/evidence').Path
$env:SAGAS_INCIDENT_OUTPUT = Join-Path (Get-Location) '.dev/incidents/example/mono-recovery'
python scripts/mono-probe.py tests/Sagas.IncidentChecks/bin/Release/netstandard2.1/Sagas.MonoIncident.dll --method RunEvidence
```

`RunEvidence` copies evidence to the new output directory before opening it,
replays outboxes, checkpoints, closes and reopens, verifies the persisted event
total, and verifies that a second replay accepts no duplicates. Keep all actual
incident data under ignored `.dev/` and out of distributable packages.
