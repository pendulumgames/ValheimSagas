# 0.3.9 transport hotfix verification

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
