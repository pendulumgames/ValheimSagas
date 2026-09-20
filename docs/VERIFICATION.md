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
