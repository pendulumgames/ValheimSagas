# Website acceptance checks

These checks use the real local HTTP API and website with the **synthetic DevHost dataset**, not a running Valheim game. They do not establish in-game compatibility.

From the repository root, keep the isolated fixture host running in one terminal:

```powershell
dotnet run --project src/Sagas.DevHost -c Release
```

In another terminal:

```powershell
npm ci --prefix tests/web
npm test --prefix tests/web
```

The runner defaults to installed Microsoft Edge on Windows. Set `SAGAS_BROWSER_EXECUTABLE` to another Chromium-based executable if necessary. On other platforms install Playwright's Chromium with `npx --prefix tests/web playwright install chromium`. `SAGAS_TEST_URL` may select another **loopback** DevHost port. The harness refuses remote URLs.

Coverage includes bearer-only authentication, the prominent fixture label, every rolling period and all-history, invalid and valid custom dates, exact per-player fixture totals, independent live presence, independent exploration with explicit linking, union versus personal exploration, canvas zoom and keyboard pan, per-character statistics, gear tooltips through keyboard/mouse/touch, quality contributions, escaped hostile names, truncated detail warnings, mobile overflow and viewport-safe tooltips. Screenshots at 1440 × 1050 and 390 × 844 are written to `.dev/qa`. Script errors and CSP violations fail the run.

An explicitly intercepted synthetic response tests hostile text rendering and event truncation; remaining checks call the running DevHost. All screenshot data is synthetic. No production token, save, world, installed mod profile, external AI service, or live server is used.

Website production assets have no npm dependencies. This package is development-only and should not be included in the distributable mod.


## Dedicated-host setup and 0.3.9 regression

Operator installation instructions are maintained in the [main README](../../README.md#dedicated-servers-ip-and-allocated-port) and the [mod-site/package README](../../docs/MOD-README.md#dedicated-servers-ip-and-allocated-port). Dedicated hosts use their allocated TCP port with `ListenPrefix = http://*:PORT/` (including the trailing slash), then browse to `http://SERVER-IP:PORT/`. A blank DisplayName uses the Valheim server name, then the world name; AdvertisedAddress is the optional displayed game-join address.

Update the host and every Sagas client for 0.3.9 transport throttling. These automated tests do not prove the reported multiplayer Steam send-limit incident is resolved. Follow the [in-game regression steps](../../docs/IN-GAME-TESTS.md#039-steam-send-limit-regression) and keep position-sharing permissions enabled when checking markers.
