# 0.3.8 verification

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
