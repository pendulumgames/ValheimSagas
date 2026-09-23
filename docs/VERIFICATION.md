# 0.3.30 large-world map verification

Full release build passed with zero errors and two existing MSB3277 reference warnings. Automated checks passed: 2,772 Core assertions, 80 synthetic lore checks, 22,017 terrain checks, 1,557 portrait/lifecycle checks, 13 optional-adapter checks and 224 installed-assembly metadata checks. New delivery coverage includes bounded pages, incremental updates, legacy migration, restart, exact masks, sharing resets, HTTP authorization and detail bounds.

The complete browser acceptance suite passed against an isolated synthetic DevHost, including desktop/mobile and optional-mod cases. Browser checks cover the existing UI plus chunk orientation, conservative fog, precise detail, cache invalidation, a settled 2,048-tile detail working set, concurrent updates and selection races. Full-world synthetic benchmark results and reproducible commands are in [MAP-PERFORMANCE.md](MAP-PERFORMANCE.md); raw results are in `docs/benchmarks`. The measured maximum for the final 1,000-pin / 40,000-zone / 5,000-event case was 24.8 ms. This is headless-browser evidence, not in-game or dedicated-host acceptance.

No live host, profile or save was changed. Required next check: install host update, refresh browser/Steam tabs, allow existing map indexing, then pan/zoom with game/player pins, SLS and both heat maps; verify discovery edges and privacy selections during continued exploration.

# 0.3.22 roster, presence and saga card verification

September 21, 2026: build passed with zero errors and two existing MSB3277 warnings. Full backend suites passed: 2,555 Core assertions, 80 lore, 22,017 terrain, 1,557 portrait/lifecycle and 212 installed metadata checks. New checks verify that the boss checklist uses distinct authoritative receipts, rejects client-supplied completion keys, clears private progress and includes all eight canonical boss labels.

Browser regression checks confirm exactly one avatar in each Online/Recorded Viking card; identical online badge text, color, background, radius, size and weight across roster/directory/profile; online/syncing/offline states; three repeated directory-to-same-character transitions without losing status; exact one-of-eight progress; saved saga excerpts, template exclusion and escaped markup; and desktop/mobile layout. Card screenshots use synthetic data and were reviewed. Existing browser suites passed, including 96 artwork combinations, authentication, character routes, map/heatmap, presence, login, optional mods and icons. The chained Nemesis fixture hit its existing initial-refresh race; an isolated rerun passed. Its fixture now waits for initial network idle before injecting controlled state, matching the presence regression checks.

The duplicate avatar came from an unintended second avatar call in the roster template. The missing character status came from clearing DOM text without resetting the HTML memo. Both are fixed at their source. New saga previews use existing privacy-filtered chapter data and never trigger generation or new API polling. Checklist fields are derived from persistent boss receipts, independently of filtered statistics. No game hooks or portrait capture changes. In-game/live-server verification is not claimed.

# 0.3.21 Viking directory and Deep North verification

September 21, 2026: build passed with zero errors and two existing MSB3277 warnings. Full suites passed: 2,552 Core assertions, 80 lore, 22,017 terrain, 1,557 portrait/lifecycle and 212 installed metadata checks. Ten new backend assertions cover final-phase boss identity, distinct/private team credit, earlier-phase exclusion, rejecting phase duration as full-fight timing, final backdrop bounds, server-derived achievement metadata independent of analytics filters, privacy clearing, retention/restart, and backfilling a pre-upgrade retained final death.

Read-only inspection of the installed SoftRef manifest and c4210710 bundle found FrozenKing, FrozenKing_p2 and FrozenKing_p3, with 10,000/7,000/30,000 health. Final prefab uses enemy_frozenking_p3 and defeated_frozenking_p3. Installed resources.assets English localization confirms Kall Fimbulbringer. No game assets or extracted data are shipped.

The complete synthetic browser suite passed. New directory checks cover signed/shared access landing, three featured profiles ordered by progression then retained boss date, private exclusions, search/empty states, keyboard navigation, direct links, mobile bounds, head portrait decoding and initials fallback. Pixel checks confirm automatic game icons are identical with Viking colors on/off while personal icons gain rings. Desktop/mobile visuals were reviewed with explicitly synthetic head portraits. Existing tests now choose a directory card before exercising character controls. The leaderboard HTTP fixture now expects eight known bosses plus its one synthetic mod boss.

No in-game Deep North encounter was performed. Only final-phase participants are credited for Kall; full multi-phase team and timer aggregation remains unimplemented, so his phase timer is not ranked as a full fight. Retained phase1/2 kills remain ordinary recorded history, but are excluded from boss leaderboards/progression. New receipt migration cannot recover already-pruned final deaths. Dedicated multiplayer validation remains pending.

# 0.3.20 atlas layout and config ownership verification

Verified September 21, 2026 against installed Valheim assemblies and Shudnal ConfigurationManager 1.1.21. Build: zero errors, two existing MSB3277 warnings. Full suites: 2,542 Core assertions, 80 lore, 22,017 terrain, 1,557 portrait/lifecycle and 212 installed-assembly metadata checks. Metadata includes the optional manager's Entry property, synchronization-label method, setter and dynamic attribute inspection entry point. Metadata checks do not execute the in-game UI patch.

The complete synthetic browser suite passed, including 96 grounding combinations and existing authentication/profile/heat-map/SLS/icon/countdown regressions. New atlas checks cover default-closed controls, four exclusive trays, unchanged map geometry, Escape/focus return, mobile width and Activity-to-filter navigation with applied Viking names and independent terrain. Desktop/mobile screenshots were reviewed. A strict zero comparison in the existing whole-world pan test now allows a 1e-6 meter floating-point tolerance; the wider canvas produced a 1.8e-12 meter residual. No map physics or clamping code changed.

Server/Lore values remain host-local. The optional UI adapter labels ownership and rejects edits in remote sessions; no credential/config serialization was added. Dynamic ReadOnly tags work with the inspected manager. In-game S/C rendering, transition between menu/host/remote client, and edit rejection still require playtesting. Confirm that privacy, notification and login-shortcut controls remain editable on clients. The mod retains no required dependency on ConfigurationManager, Epic Loot or SLS.

Both retention defaults already equal zero in source. Existing saved retention choices are intentionally preserved. Package validation and isolated Gale Test installation are recorded in local STATUS.md; no live server or save was modified.

# 0.3.17 startup attribution and SLS outline verification

September 21, 2026: build passed against installed assemblies, zero errors/two existing MSB3277 warnings; 172 installed API metadata checks passed. Existing inspected SLS configuration/data/color types and Epic Loot item extension APIs reside in their registered mod assemblies. First-use broad assembly scans were replaced only for these cached metadata lookups; Harmony death/loot hook targets remain unchanged. This is a likely startup contributor, not a measured explanation of the full 143.4/88.2 ms residuals. New first-world/first-equipment phase logs also state that JIT before method entry is outside their measured body.

Website JS syntax, 23 SLS canvas/fog/hover/absence checks and 16 heatmap checks passed. Added pixel checks for 20% alpha, duplicate-outline opacity, lower host opacity and zero opacity. Desktop1600 and mobile390 previews used an isolated recorded-data copy with aliases; before/after screenshots reviewed under `.dev/zones0317-*.png`. Host setting was50%; website now caps20% without modifying the config. No backend/schema change or fresh full Core rerun needed. Package checks and Gale Test installation are recorded locally in STATUS.md.

Next fresh launch: compare initial world/equipment warnings and `Sagas startup detail` phases, ensure SLS zone/Nemesis data and Epic Loot rarity/effects still populate, and check map opacity/fog/hover. No in-game0.3.17 performance result, dedicated acceptance or provider request is claimed.

# 0.3.16 startup work verification

Verified September 21, 2026. Final plugin build passed against installed Valheim/Unity/BepInEx assemblies (zero errors, two existing MSB3277 warnings). Full suites: 2,486 Core assertions, 80 lore, 22,017 terrain, 1,556 portrait/lifecycle and 172 installed metadata checks; final focused portrait/lifecycle rerun passed 1,557 after adding retirement-error recovery. Website JavaScript syntax passed; website UI is unchanged. Local logs: `.dev/startup0316-tests.log`, `.dev/startup0316-finalbuild.log`.

Production background-resource tests cover slow construction, nonblocking retirement, preventing simultaneous reopen, discarded startup, duplicate calls, constructor failure, shutdown failure and retry. Installed Unity Mono separately passed real worker service construction/start, draining queued synthetic player data, asynchronous disposal, and reopening the same database with persisted data and reset presence. Reproduce with `scripts/test-unity-mono.ps1 -Method RunStartup`; evidence `.dev/mono-startup-0316/report.txt`. No live game or server was altered by these tests.

Terrain tests compare byte-identical flat/strip atlas sampling across strip boundaries and clamped edges, reject partial/malformed/out-of-order strips, and retain existing fog checks. Installed metadata confirms the rectangular AsyncGPUReadback.Request overload. The GPU target is rendered once; each request copies at most 32 rows (512 KiB at 4096 width) and retains resources through callback completion on cancellation. Existing tested readback-lifetime policy is shared with portraits. Pending atlas work does not export degraded fallback tiles.

The 0.3.15 local playtest produced nine ready portraits; seven subsequent capture maxima were 3.5-3.9 ms, one 12.3 ms, initial 42.6 ms. Startup warnings were world update334.2 ms, equipment106.3 ms and exploration231.2 ms. Those are the baseline, not measurements of 0.3.16. New logs separate worker startup, atlas render/setup, strip callback copy, icon batch maximum and portrait phases. Real GPU map appearance/orientation, first-use render costs, world-change cancellation and dedicated-host performance still need the new IN-GAME-TESTS.md session. No hitch-free guarantee or paid provider call.

# 0.3.15 staged portrait verification

Verified September 21, 2026. Plugin compilation passed against installed assemblies: zero errors and two existing MSB3277 reference-unification warnings. Full regression suites passed: 2,486 Core assertions, 80 lore checks, 21,964 terrain checks, 1,544 portrait/pixel/scheduling/resource-lifetime checks, and 171 installed-API metadata checks. Website JavaScript syntax passed; website UI assets are unchanged. Local evidence: `.dev/staged0315-verified.log`.

New checks exercise no periodic refresh, two-second debounce/ten-second cooldown, sitting/standing deferral, cancellation ownership until all callbacks complete, partial submission failure, per-frame timing aggregation and consent-aware saved-portrait retention. The installed Humanoid sitting override, Player attachment API and AsyncGPUReadback signatures were inspected directly. These checks do not execute the GPU capture pipeline.

The isolated installed-Unity-Mono HTTP probe passed persistence, portrait/media delivery, public/private access, optional integrations, login and encrypted Saga Profile keys. Updated its old key fixture to create a named Saga Profile. Its missing MonoPosixHelper warning exercises the existing identity-compression fallback; its deliberately injected HTTP500 remains observable with a correlation ID. No game instance or real provider request ran. Evidence: `.dev/mono-http-0315-final/report.txt`.

Unity requires readback data to be consumed during its valid callback frame and temporary targets to remain alive until completion: [AsyncGPUReadbackRequest](https://docs.unity3d.com/ja/current/ScriptReference/Rendering.AsyncGPUReadbackRequest.html), [AsyncGPUReadback.Request](https://docs.unity3d.com/jp/current/ScriptReference/Rendering.AsyncGPUReadback.Request.html). Sagas copies pixels in callbacks and retains textures/model resources through cancellation. There is no blocking readback fallback. The existing detached-array PNG encoder remains on a background task.

Preparation yields between bounded work units; black/white render pairs run together to preserve shader-time consistency. Individual native render, pose-snapshot and callback-copy operations still run on the main thread. No percentage improvement or hitch-free guarantee is claimed. Actual image orientation, framing, body/effects, sitting suppression, cancellation behavior and maximum frame cost require the new IN-GAME-TESTS.md playtest, followed by dedicated-server acceptance.

# 0.3.14 portrait optimization verification

Verified September 20, 2026. Build passed with zero errors and two existing MSB3277 warnings. Full suites passed: 2,484 Core assertions, 80 lore checks, 21,964 terrain checks, 1,520 portrait/pixel/scheduling checks, and 166 installed-assembly metadata checks. Final plugin build also passed after preserving the simplified-portrait status.

New checks cover appearance debounce, capture cooldown, ten-minute fallback, failure retry, reset, alpha-aware bounded downsampling, and the installed appearance fields/EncodeArrayToPNG signature. Existing body/alpha/frame and render-state restoration tests still pass. Native rendering and background native PNG output are not executed by these synthetic tests; in-game image orientation, visual quality and measured frame-time savings remain to be checked.

The installed ImageConversionModule was inspected directly. Unity documents [EncodeArrayToPNG as thread-safe](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/imageconversion/encodearraytopng). Only detached managed pixel arrays and primitive metadata enter the worker; the Texture2D and camera stay on the main thread. The NativeArray encoder and asynchronous GPU readback are not used.

An unchanged character's safety-refresh interval increases from one to ten minutes (roughly 90% fewer scheduled refreshes), not a measured 90% reduction in frame time. Each remaining capture logs main-thread stages and background processing separately. See IN-GAME-TESTS.md for acceptance steps. No live-server update or provider requests.

# 0.3.13 periodic-stall candidate verification

Verified September 20, 2026. Local 0.3.10 logs exposed recurring main-thread work despite no Steam send-limit errors. This release addresses those paths; no in-game frame-time improvement has yet been verified.

- Plugin builds against installed game/BepInEx references: zero errors, two existing MSB3277 warnings. Passed 2,484 Core assertions, 80 lore checks, 21,964 terrain checks, 911 portrait checks and 150 installed-assembly metadata checks.
- Shared production SLS capture code tested with 40,000 synthetic zones, bounded batches, changed levels/palettes, detached results, absent zones, collection invalidation and the size limit. .NET run: 313 slices, slowest 1.04 ms. These timings are not a guarantee of in-game performance.
- The same capture tests ran under the installed Unity Mono runtime without launching Valheim: 321 assertions, 314 slices, slowest 6.65 ms including first-use warmup. Reproduce with `dotnet build tests/Sagas.IncidentChecks/Sagas.MonoIncident.csproj -c Release` then `python scripts/mono-probe.py tests/Sagas.IncidentChecks/bin/Release/netstandard2.1/Sagas.MonoIncident.dll --method RunSlsCapture`.
- Existing website acceptance remains unchanged from 0.3.12. No paid provider calls, live-server deployment, game-save edits or mod-site publication.
- Follow docs/IN-GAME-TESTS.md for local and dedicated-server retesting. Portrait capture is now timed separately; once-per-minute warning suppression is not a complete frame profiler.

# 0.3.12 Appearance and Saga Profiles verification

Verified September 20, 2026 against installed assemblies and an isolated synthetic DevHost; no in-game session or real OpenRouter request.

- Plugin build: zero errors, two existing MSB3277 warnings. All 2,164 Core assertions, 80 lore checks, 21,964 terrain checks, 911 portrait checks and 150 installed-assembly metadata checks passed.
- Profile credential tests cover independent keys, switching, renaming, removal isolation, no plaintext secrets in HTTP/database output, malformed requests and restart persistence. A focused run passed 28 assertions.
- Full browser acceptance passed, including 96 grounding combinations, readable profile links, heat maps, login/scenery permissions, personal storyteller settings, account menu, optional-mod overlays and responsive layouts.
- New Appearance checks at 1440, 390 and 320 pixels verify collapsed controls, layout switching, owner-only scenery persistence, other-player restrictions, Escape and no horizontal overflow. Mobile Appearance and Saga Profiles screenshots were visually reviewed.
- Follow-up compact Appearance styling: appearance and login browser suites rerun successfully; 390/320px screenshots visually reviewed. Backend unchanged.
- Local package validation checks asset hashes and rejects malformed archives. No live profile/server installation or mod-site publication is part of this update.

# 0.3.11 account and storyteller interface verification

Verified September 20, 2026 using Edge and an isolated synthetic DevHost. This is website/browser verification, not an in-game session.

- Plugin version build: zero errors, two existing MSB3277 warnings. Backend behavior/schema is unchanged from0.3.10; JavaScript syntax passes.
- 63 new account checks at1440px,390px and320px: authenticated saved-head avatar, Login/icon states, account keyboard/focus/Escape/outside dismissal, separate dialogs, simple host default, personal mode/missing-key error, failed settings-load save protection, immediate unsaved-key cleanup and logout. A clearly synthetic portrait response is used.
- Real local HTTP login tests: invalid token feedback, shared-token read-only access/personal login upgrade, scenery ownership and logout. Storyteller tests cover saved setups, paid opt-in validation, secret redaction, removal and mobile layout.
- Existing full website acceptance,96biome grounding combinations, profile links,16heatmap regressions, readability,19SLS and8Nemesis UI checks passed. The full run stopped at an Escape cleanup race in the new test; after fixing synchronous cancel cleanup, account/login/storyteller tests and the remaining SLS/Nemesis suites were rerun and passed.
- Visually reviewed desktop account menu/default settings and390px/320px settings screenshots in .dev/qa. Responsive dialogs stay within the viewport and scroll for longer personal settings.

No credentials, private data or generated fixtures are bundled. No paid requests or live installation updates occurred. Core/lore/terrain/portrait tests below remain the evidence for unchanged0.3.10 backend code; they were not redundantly rerun for this website-only change.

## Previous 0.3.10 verification

Verified September 20, 2026 on Windows. No live OpenRouter generation, credentials, charges or multiplayer session were used.

- Installed-assembly build: zero errors, two existing MSB3277 reference-unification warnings.
- Core: 2,154 assertions. New synthetic worker coverage uses one host key/preset for two default character sagas and one server saga, checks omission of provider overrides, shared daily attempt accounting, actual-model provenance, restart persistence and absence of the key from viewer state.
- Lore: 80 synthetic HTTP checks. Paid host models/presets omit the provider object; free host mode retains zero price ceilings; personal paid requests keep validated explicit ceilings. Requests still bound output and disable tools/plugins.
- Terrain: 21,964 checks; portrait: 911 checks; installed API metadata: 150 checks; JavaScript syntax passes. Website assets/flows are unchanged from the previous browser verification.
- Official OpenRouter documentation was checked for shallow preset merging/provider price limits, and the anonymous model catalog listed `deepseek/deepseek-v4-flash-0731`. A live preset/account billing check is still an operator task.

The previous 0.3.9 transport hotfix remains included and still requires the multiplayer acceptance steps below. Updating the host configuration does not silently enable paid use: AllowPaidModels defaults to false. For opted-in paid presets, monetary restrictions are controlled in OpenRouter, not imposed a second time by Sagas.

## Previous 0.3.9 verification

Verified September 20, 2026 on Windows. This is a candidate fix for the reported multiplayer three-second lag / Steam `k_EResultLimitExceeded` incident, not a claim of in-game resolution.

- Plugin compiles against installed Valheim/BepInEx: zero errors; two existing MSB3277 reference-unification warnings.
- Core: 2,148 assertions, including a synthetic ten-minute saturated transport simulation. All four traffic classes make progress, byte allowances hold, busy socket queues stop sends, no two sends occur within 250 ms, and a maximum-size portrait completes inside its assembly lifetime despite simulated congestion.
- Lore: 69 synthetic HTTP checks; terrain: 21,964 checks; portrait processing: 911 checks; installed assembly metadata: 150 checks, now including `ZNetPeer.m_socket`, `ISocket.GetSendQueueSize()` and the string type of `ZNet.m_ServerName`.
- Isolated installed Unity Mono HTTP/persistence/media/privacy probe passes. It does not launch a game or exercise a live Steam connection.
- The actual installed `ZSteamSocket` implementation was inspected read-only: failed sends remain at the head of its queue and are attempted again each frame; queue size includes engine and Steam pending/unacknowledged bytes. Sagas now checks that queue before adding telemetry.
- Host display-name fallback now reads the inspected Valheim server-name field; an explicit Sagas DisplayName still takes precedence. Dedicated-server UI behavior requires the operator retest.
- Website code/artwork is unchanged; the previous browser results below apply to that unchanged website. No new browser or live multiplayer verification is claimed for this hotfix.

Required before release: update host and all clients, restart connections, and complete the 0.3.9 Steam send-limit regression steps in IN-GAME-TESTS.md. Initial terrain/portrait loading is deliberately slower. Transport throttling cannot eliminate congestion caused by other mods or the network, and does not remove data already queued by an older running client.

## Previous website and feature verification (0.3.8)

Verified September 19, 2026 on Windows. Automated fixtures are synthetic unless explicitly identified as recorded snapshot screenshots.

- Plugin compiles against the installed Valheim/BepInEx assemblies: zero errors; two existing MSB3277 assembly-unification warnings.
- Core: 515 assertions covering filters, provenance, persistence, privacy, telemetry queues, HTTP authentication, personal logins, progression, encrypted storyteller settings and restart behavior.
- Lore: 69 synthetic HTTP checks covering rich evidence, identity scrubbing, continuity, consent, free/paid limits, presets, legacy-template migration, retry/cancellation and immutable facts. No real OpenRouter requests or charges.
- Terrain: 21,964 synthetic exploration-mask/coherent-atlas checks.
- Portrait processing: 911 synthetic alpha, emission, framing and render-state checks. These do not exercise GPU character capture.
- Installed assemblies: 147 metadata/API checks, without executing gameplay hooks.
- Edge browser: complete synthetic end-to-end suite, profile links, 16 heatmap regressions, readability, real local HTTP personal-login flow and personal storyteller controls. Optional SLS adds 19 canvas/hover/absence checks, 8 portrait/metric/leaderboard UI checks, 38 backend zone/privacy checks and 20 Nemesis metric/lore checks (backend included in Core count). Includes 96 biome/mode/viewport grounding cases and viewport sizes through 4K, with synthetic portraits. Brown fog is tested for opacity, stable redraws and textured color variation.
- Installed Unity Mono: isolated HTTP, persistence, media transfer, privacy, login and AES/HMAC key roundtrip, SLS fog snapshot, Nemesis score/team metric and progression-isolation probe. This uses Valheim's Mono runtime in a separate process, not a running game.
- Package: exact file allowlist, dependency manifest, icon dimensions, screenshot links, artwork dimensions, SHA-256 inventory and rejection of nine deliberately invalid archives.

README screenshots show an isolated copy of recorded playtest data with display names replaced. Proprietary runtime assets are not shipped separately. The package excludes configs, credentials, databases, game DLLs, extracted textures and development fixtures.

Still required: operator-controlled credentialed OpenRouter/preset execution, two-client multiplayer acceptance and Linux hosting. No in-game verification of 0.3.8 is claimed. See IN-GAME-TESTS.md for exact scenarios.


## 0.3.18 map icons and daylight (September 21, 2026)

Installed assembly inspection confirms Minimap.PinData fields, m_pins, personal exploration storage/coordinate conversion, and EnvMan.GetDay/GetDayFraction (including the game's rescaled smoothed day/night phase). No world-location discovery scan or game asset distribution.

Full automated run: 2507 Core assertions, 80 lore, 22017 terrain, 1557 portrait/lifecycle and 187 installed metadata checks. Final focused map suite: 31 assertions including independent HTTP endpoints, private-host auth, map-only sprite visibility and media-kind isolation, personal consent, opaque/transparent fog, selected owners/world isolation, offline persistence, empty/deleted snapshots, out-of-order multipart atomicity and lost-ACK recovery. The initial new fog fixture set the wrong alpha pixel; corrected before passing. A simultaneous focused build hit a test executable lock; rerunning after the full suite completed passed. Final compilation: 0 errors, 2 existing MSB3277 assembly-resolution warnings.

Website: full synthetic acceptance/96 grounding combinations, readable profile links, 16 heatmap checks, readability, owner login/storyteller, 63 account checks, Appearance, 23 SLS checks and 8 Nemesis checks passed. New 19-check real HTTP/browser suite covers independent layers, host clock, both graphics/None, saved preferences, escaped names, explorer changes, stale world context and 390px mobile overflow; desktop/mobile captures visually reviewed in .dev/map0318-*.png. Initial remaining account runners needed SAGAS_LOGIN_FIXTURE pointed at isolated fixture0318; rerun with that path passed.

Runtime sprite capture, actual pin classification/positions, clock behavior on a dedicated host and game-frame cost remain **unverified in game**. First pin/icon availability depends on personal terrain import and upload pacing. Cap 2048 pins and 128 unique map sprite hashes per client session; dynamic pings/shouts/player icons are excluded because presence has its own layer. No actual game/save/config/live-server edits during tests. Package/install results recorded in local STATUS.md.


## 0.3.19 Viking colors, mod pins and countdown

Inspected installed EpicLoot.Adventure.MinimapController: registers custom sprite-backed Minimap types800/801 and adds transient bounty/treasure pins with ordinary AddPin. Inspected Jotunn.MinimapManager: its texture/drawing overlays require a dedicated adapter. Verified all18installed vanilla PinType integer values; total208metadata checks including EnvMan day length, skip state and ZNet player count. Neither optional mod is a dependency.

Full build:0errors/2existingMSB3277warnings;2541Core/80lore/22017terrain/1557portrait-lifecycle passed. After final clock-response and outside-fog media updates,56focused map/HTTP/color/policy/time assertions passed. Color tests cover10distinct perceptually separated assignments,32without immediate palette reuse, client override rejection, rename/presence/restart stability and world scope. Pin tests preserve all game-location sharers, owner filtering and personal consent above fog while blocking unknown automatic locations. Time tests cover midnight wrap,15%/85%day thresholds,custom day length,time scale,empty-host pause,sleep and invalid input. Actual installed Unity Mono worker startup/storage/reopen probe passed with synthetic data; this does not run graphics or game hooks.

Browser: synthetic full acceptance/96grounding combinations, links, heatmap16, readability, login/storyteller/account63, Appearance, SLS23 passed. The Nemesis UI runner encountered one element-wait timeout in the chained run; isolated rerun passed8checks without source changes. New map19 and Viking-map19checks pass, including larger icons, multi-owner filter semantics, personal fog behavior, real-time countdown, client clock skew, saved options and390px layout. Desktop/mobile previews reviewed under .dev/map0319-*.png. New real native pins/countdown/color stability and dedicated multiplayer frame-time acceptance remain unverified in game.


### 0.3.23 ? Steam shortcut and map clusters

Installed Steamworks.NET signatures inspected directly and added to214installed metadata checks. Plugin compiles with0errors and2existing assembly-unification warnings. Full2555Core/80lore/22017terrain/1557portrait-lifecycle checks passed;13additional URL validation checks passed after extracting shared URL policy, followed by final plugin rebuild. Rejects non-HTTP schemes, embedded credentials, query/fragment, wildcard/unspecified hosts and oversized addresses. No game assets or Steam DLL distributed.

Browser suites passed across the chain and remaining focused runs. Updated old acceptance assertion to the newly requested roster order. Isolated SLS extraction fixture gained an empty cluster stub for its new hover dependency;23checks passed. New cluster tests use real canvas click,30coincident pins, escaped picker, exact position, filters,400randomized locations with separated badge centers,2048dense benchmark (~3?4ms) and mobile width. Desktop/mobile previews are synthetic. Existing map19/Viking19/Nemesis8/tray/directory/roster checks passed.

Steam overlay opening and dedicated host public-URL RPC still require actual in-game acceptance. Metadata/compilation do not prove Steam overlay availability on a user's machine. No real OpenRouter calls, live host changes or game-save edits.


### 0.3.24 ? optional Jewelcrafting

Read installed Gale Jewelcrafting2.0.10 assemblies only. Full2584Core,80lore,22017terrain,1557portrait/lifecycle assertions passed; final13synthetic production-adapter contract checks(11present/2absent),224installed metadata checks. Final plugin compiles with0errors and2existing MSB3277 warnings. New Core tests validate bounded socket metadata, both mods together, ownership/privacy on gem-only media, offline/restart artwork, late-drop provenance reconciliation and duplicate suppression, and socket saga context without API calls.

Entire browser chain passed. Added synthetic Jewelcrafting gear with keyboard/tap/hotbar tooltip access, runtime-media request/decode using a synthetic PNG, range disclaimers, null sockets, ring placement, JC color / Epic rarity color precedence, absent sockets, escaping, distinct map loadouts and mobile width. Desktop/mobile screenshots reviewed; fixture is clearly labelled and contains no copied JC assets.

Pending real gameplay: Jewelcrafting-generated socketed creature drop and pickup; ring/necklace extra-slot and armor snapshots; socket replacement and portrait refresh; actual gem icon capture and offline persistence; Epic Loot/JC co-installation; two-client ownership and frame timing. Metadata inspection and fake-API tests do not execute Jewelcrafting Harmony hooks or GPU capture.

### 0.3.25 ? map pin interactions and optional-mod statistics

Full automated build: 2,586 Core assertions, 80 lore checks, 22,017 terrain checks, 1,557 portrait/lifecycle checks, 224 installed-assembly metadata checks, and 13 Jewelcrafting adapter checks passed. Plugin compilation: zero errors, two existing reference-unification warnings. Installed BepInEx KeyboardShortcut.Equals implementation inspected for exact-default migration.

Complete browser suite passed. New checks cover no optional mods, Epic Loot, Jewelcrafting and all three integrations; near-coincident pins at maximum zoom; actual double-click; individually selectable fan icons; expiry of selection halo; gathering on zoom-out; neighboring-fan separation; reduced motion. Dense 2,048-pin clustering measured approximately 3.2ms in this synthetic browser run. Terrain and heat are cached during bounded pin animation frames, with no added polling.

Updated atlas screenshot uses an isolated copy of the closed Test profile's saved data, with names aliased. Personal pins have one owner's color, game icons remain original, last-known position is labeled. No synthetic encounters or additional exploration were added. No new game runtime or dedicated-server acceptance is claimed; test the shortcut migration, always-on notifications, and updated map in both Gale profiles.

### 0.3.26 ? landscape zoom limit

Plugin build: zero errors, two existing reference warnings. Focused browser suites passed: clusters, heatmap (16), map details (19), Viking map (19) and atlas layout. New assertions verify the 20m scale bar via cluster double-click, normal/keyboard zoom and direct draw constraints; 40m opening/50m closing with a stable intermediate band; nearly coincident pins remain selectable. Existing fade, gather, collision, reduced-motion and mobile checks remain passing. No core/gameplay behavior changed and the full backend suite was not rerun. In-game playtest remains pending.

### 0.3.26 publication artwork refresh

Atlas screenshot recaptured and visually reviewed with the production day/night dial and countdown. Terrain/pins/position come from the same isolated playtest copy; the clock alone uses a clearly labeled example because live clock state is not persisted. Both READMEs disclose this. No runtime code changed.

Packaging requirements rechecked against https://hexium.gg/packaging and https://wiki.thunderstore.io/mods/creating-a-package: both accept this ZIP layout; Hexium accepts Thunderstore-compatible packages and strips the BepInExPack_Valheim dependency itself. Upload as a client-and-server mod. The release remains a preview: a fresh two-client dedicated-server transport/performance test and Linux-host acceptance are still outstanding; real credentialed OpenRouter execution is not part of automated verification.


### 0.3.27 verification and deployment

Full build/check run: .dev/build0327-release.log passed 2711 Core assertions,80 synthetic lore checks,22017 terrain checks,1557 portrait checks,13 optional-adapter present/absent checks and224 installed-assembly metadata checks. Final plugin rebuilt after the remaining startup guard/login serialization refinements: .dev/build0327-plugin.log, zero errors/two existing MSB3277 reference warnings. Final isolated network suite .dev/network0327/checks.log passed1757 assertions (includes next-fragment total headroom). No external OpenRouter calls, game execution, or dedicated performance acceptance claimed. Website assets unchanged; JavaScript syntax checked by build.

Final Steam total headroom includes the next4096-byte allowance (total<=28672), rather than checking the existing backlog alone. Pending<2048 and estimated delay<20ms remain required. Interleaved per-lane transfers retain progress without waiting for an entire large portrait packet. Login jobs are serialized per peer to avoid overlapping token rotation. All user-facing login notices are logged without credentials.

Next: update server and clients together to0.3.27, restart, join dedicated world, wait for character replication, press LeftCtrl+Insert and paste into website within2minutes; check LeftCtrl+Home with the public WebsiteUrl configured. Verify presence/map/gear and queued events begin arriving; swap gear and measure client/server frame times while portrait uploads. Capture both logs, including Steam pending/unacknowledged/delay and recovery lines. Crossplay remains conservative and requires separate validation. No Deep North/live-server modifications were made.


## 0.3.28 concurrent logins and compressed exploration

User reports Chrome revoked when issuing Steam login and public dashboard stuck retrying rejected credentials; initial historical map import extremely slow. Replaced single credential row with bounded32 hashed credentials/world/player; explicit Ctrl+End revokes all, browser logout only clears that browser. Persistent localStorage login and same-browser storage-event reload; separate browsers/devices deliberately require initial login. 401 recovery clears rejected credential and retries public GET once; private mode clears via reload/login prompt. Existing credentials remain valid until revoked/evicted; no token plaintext in database or URLs.

V4 wire packets add compression flag and bounded Deflate decoding (128000-byte output cap). Compression runs in up to4 existing per-lane pending jobs, immutable JSON only, no Unity work in worker. Existing250ms send spacing/bandwidth caps retained. Map scan runs at250ms cadence when queue has headroom (max2pending map packets), with256 historical candidates/pass, alternating nearby/history and one actual pixel capture/pass including unchanged partial cells. This avoids repeated nearest partial tile starving history. Entire personal exploration imports gradually; cartography-only bits still excluded. Both endpoints need0.3.28.

Read-only .dev/map0325 saved playtest clone sampled200terrain records:594415JSON bytes ->263416compressed bytes,2.26x reduction; encode+decode9ms total on development .NET, not Unity performance evidence. Compression preserves all pixels/fog exactly; no asset quality downgrade. New tests cover concurrent/revoked/bounded credentials, multi-browser remembered login/public401recovery/private401prompt, compression roundtrip and decompression size limits. Dedicated/crossplay timing still needs real playtest.


0.3.28 verification: full backend build/checks passed2720Core assertions,80synthetic lore,22017terrain,1557portrait,13optional-adapter and224installed-metadata checks (.dev/build0328-full.log). Final plugin rebuild after bounded map-scan refinement passed zero errors/two existing reference warnings (.dev/build0328-final.log). Entire web suite passed (.dev/web0328.log), including7new multi-browser/revocation/public/private recovery scenarios, desktop/mobile map/profile/account coverage. Synthetic HTTP only; no paid API calls. Real map benchmark uses read-only isolated old playtest clone, never active profile/database. Current Valheim process remains running; no profile replacement or live-server modification performed for0.3.28.

## 0.3.29 loot metadata / InventorySlots stacking fix

New drop provenance is stored on the ground ZDO under sagas.dropOrigin/sagas.dropSource, not ItemData custom data. Legacy ground tags migrate on owner Load and persist once; non-owner event capture can read legacy tags until owner migration. Inventory AddItem/CanAddItem prefixes remove only the two obsolete keys before InventorySlots checks (explicit HarmonyBefore). Inventory.Load also removes them from loaded items. No broad metadata clearing, periodic inventory scan, offline save rewrite, or optional inventory dependency. Existing ground merges retain conservative unknown provenance; server quantity ceilings and deduplication remain unchanged.

Verification: full build passed with zero errors and two existing MSB3277 warnings; 2732 Core assertions, 80 synthetic lore, 22017 terrain, 1557 portrait, 13 optional-adapter and 224 installed metadata checks (.dev/build0329-full.log). Eight additional checks invoked the actual installed InventorySlots 1.5.11 StackMetadataPolicy via reflection using synthetic dictionaries and production cleanup (.dev/inventory0329.log): reproduced failures before cleanup and successful auto/manual compatibility afterwards, while retaining foreign metadata restrictions. No Unity/Harmony runtime execution or multiplayer verification claimed. Website source unchanged; JavaScript syntax and package checks used rather than repeating browser screenshots.

Reproduce optional installed-DLL checks with: dotnet run --project tests/Sagas.InventoryChecks -c Release -- <InventorySlots.dll>. Full build also accepts -InventorySlotsDll <path>. DLL is an external test input, never distributed. In-game next steps are in docs/IN-GAME-TESTS.md: update server and all clients, restart, load existing character/containers, test automatic/manual stacking and transfers, then two-client drop/collect/redrop dedup. Existing separate stacks may require normal manual stacking after cleanup.