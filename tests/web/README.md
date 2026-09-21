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


For 0.3.10 host-funded saga presets, see [host setup](../../README.md#host-funded-models-and-openrouter-presets). Free is the default; AllowPaidModels explicitly enables spending, and paid host preset pricing is managed in OpenRouter. Automated fixtures do not spend credits.


0.3.11 adds `account.cjs`: real local login/session/settings API with a clearly synthetic portrait response, desktop/390px/320px layouts, account menu keyboard/focus/outside dismissal, separate login/settings dialogs, host-default simplicity, missing-key feedback, failed-load save protection and secret clearing/logout. Screenshots are written under .dev/qa and are not game captures.

The separate `node tests/web/map-details.cjs` check uses the real synthetic map-icons/clock endpoints, independent browser toggles, saved clock styles, safe pin names, filter/world context isolation and desktop/mobile layout. Fixture sprites use fallback symbols; native game sprite extraction still requires an in-game test. When the DevHost data directory differs from `.dev/fixture`, set `SAGAS_LOGIN_FIXTURE` to its `synthetic-logins.json` for the owner/account test runners.

`viking-map.cjs` checks per-Viking icon ownership colors and persistent per-world filters, multiple owners on shared game pins, annotations above fog, larger icon hit targets, independent terrain/presence, countdown/clock-skew/pause/stale behavior and responsive layout. The tests use synthetic records and no game assets.
