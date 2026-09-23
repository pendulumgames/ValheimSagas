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


The atlas opens with option trays closed. `atlas-controls.cjs` opens each public tray before the existing regression checks interact with its controls. `atlas-layout.cjs` verifies initial map-first state, exclusive trays, unchanged canvas geometry, keyboard dismissal/focus, Activity-to-filter navigation, applied player scope, independent terrain, and mobile width. Screenshots use explicitly synthetic terrain, not a game map.

`viking-directory.cjs` checks owner-independent search landing, three progression/recent-victory recommendations, private-profile exclusion, saved-head media decoding using synthetic portrait pixels, keyboard and direct profile navigation, empty/mobile states, and pixel-identical game icons when Viking colors are toggled. Personal pins must still gain their color ring.

`roster-cards.cjs` catches duplicate avatars in both World rosters, compares shared presence styles/states across roster/search/profile, revisits the same unchanged profile three times, and verifies exact boss receipts, saved saga previews, template exclusion, escaping and mobile layout. Controlled-state fixtures wait for initial network idle before freezing snapshots to avoid the initial world-selection refresh overwriting synthetic values.

`map-clusters.cjs` verifies atlas/roster document order, actual click-to-open clusters, dense and coincident pins, escaped keyboard/tap picker, exact-coordinate selection, layer filtering, collision-free badge centers and mobile width. Logs browser-only dense2048 timing; this is not a Unity/game timing test.

`jewelcrafting.cjs` injects explicitly synthetic socketed gear into the local fixture, decodes a synthetic gem PNG, tests filled/empty slots and configured ranges, ring placement, mouse/keyboard/tap hotbar access, Epic Loot coexistence/palette priority, no-socket fallback, escaped text, distinct map socket loadouts and mobile width. No Jewelcrafting assets or real provider requests.

`mod-awareness.cjs` verifies optional panels for vanilla, Epic Loot, Jewelcrafting and combined integrations. `map-clusters.cjs` additionally checks close-pin fan hit testing, actual double-click zoom, timed halo expiry, zoom-out gathering, adjacent fan separation and reduced motion.


For large-world performance, run `node tests/web/map-stress.cjs .dev/map-stress0330.json` from the repository root. It uses the local production browser assets with synthetic 76,648-cell exploration and up to 1,000 pins, needs no DevHost, and blocks external requests. It prepares the overview in bounded slices before measuring twelve pan/zoom redraws per scenario, including SLS and heat maps. See `docs/MAP-PERFORMANCE.md` for limitations. The normal suite also includes `map-renderer.cjs` and `map-delivery.cjs` correctness/race checks.
