# Jewelcrafting adapter contract checks

Runs the production optional adapter against clearly synthetic Unity/Jewelcrafting API-shaped objects. No game or third-party DLL executes. This tests absence, socket-only gating, empty slots, localized plain names, configured ranges (not midpoint rolls), nonfinite data, bounded metadata, optional failures/rate-limited diagnostics, cached icon callbacks, jewelry deduplication and armor classification.

Run `dotnet run --project tests/Sagas.JewelChecks -c Release` and repeat with `-- --absent`. The reproducible build runs both. Actual installed metadata is separately checked by Sagas.ApiChecks; set `SAGAS_JEWELCRAFTING_DLL` to the installed mod DLL when using a different profile path. These checks do not replace gameplay, Harmony execution, multiplayer ownership or frame-time tests.
