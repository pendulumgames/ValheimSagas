# Changelog

## 0.3.20

- Make the atlas full width with compact Terrain, Vikings, Activity and Icons trays; retain map position/zoom while changing options, with keyboard close and mobile layouts.
- Replace ambiguous adventure filters with an explicit Statistics & heat maps drawer and active scope summary. Terrain, live positions and icon ownership remain independent.
- Label host-owned and personal settings in Shudnal ConfigurationManager. Lock local hosting controls during remote sessions; never synchronize host secrets. No new required mod or network feed.
- Confirm both history-retention defaults are zero/unlimited; preserve existing configured retention values and document how to change them.

## 0.3.19

- Increase game map icons from 28 to 36px; add optional matching Viking colors for pin rings, player markers and names, plus independent per-Viking icon checkboxes. Store world-specific server-owned colors once and choose separated new colors without recoloring existing players. Preserve all sharers on deduplicated game locations.
- Shared personal annotations now appear above fog, including undiscovered coordinates, only with ShareMap and SharePlayerPins consent. Automatic game/mod locations remain personally exploration-gated.
- Include sprite-backed custom Minimap pin types, including inspected Epic Loot bounty/treasure pins. Unknown saved custom pins require personal consent; separate drawing overlays need adapters.
- Replace static phase text with real-time estimates until day/night, honoring host day length/time scale and showing paused, skipping or overdue states. One-second browser countdown does not reload the map.


## 0.3.18

- Add separately loaded discovered game pins and opt-in personal map pins, runtime game sprites, browser layer toggles and hover/tap/keyboard-list inspection. Enforce owner sharing and personal fog on the server; exclude copied cartography pins. Preserve offline pins, deduplicate shared game locations, atomically replace multipart snapshots and retry lost acknowledgements.
- Bound pin scanning to 16 entries per frame, encode snapshots off-thread, share existing upload budgets and capture at most one item/map icon per frame across both queues. No proprietary map artwork is bundled.
- Add two original host-synchronized daylight graphics (sun/moon dial and horizon) and None, with persistent per-browser preferences and stale/unavailable states.
- New automated privacy, persistence, multipart, HTTP, API and browser checks. Real in-game pins, icon appearance and frame-time impact still require playtesting.

## 0.3.17

- Reduce website SLS zone outlines to at most 20% opacity, respecting lower host values. Composite the layer once so shared/overlapping boundaries do not become darker; preserve level colors, hover/tap details and fog rules. In-game SLS settings are unchanged.
- Resolve initial SLS and Epic Loot metadata inside each registered plugin assembly instead of searching all loaded assemblies. Add limited world/equipment startup phase logs to distinguish remaining initialization costs; real startup timing gains still require a fresh playtest.

## 0.3.16

- Construct host storage and website services on a worker. Keep local telemetry pending until adoption; retire abandoned startup and drain active storage off-thread before reopening the database.
- Read the coherent world atlas asynchronously in bounded strips, avoiding the full-image synchronous GPU readback and 64 MB main-thread copy. Export only complete, personally fog-masked tiles; pending capture never publishes temporary fallback tiles. Preserve explicit degraded terrain fallback on unsupported/failed readback.
- Queue uncached item icons and render at most one per frame, outside equipment snapshots and active portrait/map preparation. Equipment can arrive before its icons.
- Log atlas setup/readback, icon batch maximum, outbox startup and per-phase portrait costs. Startup timing improvements still need a fresh game session; individual Unity render calls remain on the main thread.

## 0.3.15

- Spread portrait preparation, bone copying, equipment effects and visibility/framing passes across frames. Freeze the character pose before preparation; retain native skinned rendering and weapon effects.
- Replace synchronous portrait pixel readback with bounded asynchronous GPU requests. Copy callback data into detached arrays, then reconstruct transparency and encode PNGs in the background. Keep GPU resources alive until callbacks finish, including cancellation and partial submission failures. Unsupported graphics devices retain the saved portrait instead of falling back to blocking readback.
- Capture only after appearance changes settle: two-second debounce and ten-second minimum cooldown, with no periodic refresh. Defer seated/attached poses until standing and settled; keep the last shared portrait during deferred capture and upload. Profile privacy revocation still removes access.
- Add per-frame capture-work timing. Automated checks cover scheduling, resource lifetime, pixels, persistence and installed APIs; real GPU appearance and frame-time improvements require a playtest. Individual Unity render operations still execute on the main thread.

## 0.3.14

- Refresh portraits after settled equipment/cosmetic changes, with a two-second debounce, ten-second capture cooldown and ten-minute safety refresh instead of rebuilding every minute. Failed captures retry no faster than once per minute.
- Move final-resolution transparency reconstruction, size-limit fallback, PNG encoding and hashing to a single background job using detached pixel arrays and Unity's thread-safe array encoder. Keep rendering/readback synchronous on the game thread; no async GPU readback or multi-frame render preparation.
- Retain the last good portrait during processing; reject outdated results after appearance, character, world or consent changes. Preserve normal 1024x1536 output and body visibility checks.
- Log preparation, visibility/framing, final render/readback and background processing timings separately. In-game appearance, orientation and frame-time confirmation remain required.


## 0.3.13

- Address periodic game-thread stalls: cache Epic Loot and SLS reflection metadata instead of repeatedly searching assemblies and members. Runtime item effects, rarity colors and settings are still read fresh.
- Capture SLS zones incrementally (up to 128 zones per frame with a 2 ms target), publishing only complete detached snapshots. Run snapshot validation/copying on a bounded background task; Unity/mod reads stay on the main thread.
- Add separate portrait timing warnings to distinguish occasional image capture from equipment work. Preserve upload backpressure and pacing.
- This is a performance fix candidate; a new in-game and dedicated-server playtest is still required.


## 0.3.12

- Group page layout and owner-only Viking scenery in a compact Appearance popover, with clear personal-view versus public-profile labels. Keep the small trigger beside profile tabs on mobile and use a clean scenery selector with a compact save action.
- Replace saved setups with Saga Profiles, each containing its own encrypted OpenRouter key and model/preset. Switching profiles switches credentials; deleting a profile removes its credential.
- Keep paid opt-in, request limits, HTTPS key submission and server storyteller defaults.


## 0.3.11

- Replace the signed-in Login button with a saved-portrait account icon and a compact Saga Settings / Log out menu. Preserve anonymous public browsing and shared-token read-only access.
- Separate token login from storyteller settings. Start with Server storyteller or My own storyteller, reveal personal key/model controls only when needed, and place saved setups/request limits under advanced options.
- Preserve existing setups and spending controls, add clearer errors and keyboard/focus behavior, and clear unsaved secrets when dialogs close.

## 0.3.10

- Add host AllowPaidModels opt-in for direct models and OpenRouter presets, funding default Viking and shared server sagas with the host key. Free routing remains the default.
- Preserve paid preset pricing/provider settings by omitting Sagas provider overrides; manage monetary limits in OpenRouter, with the existing shared daily attempt budget in Sagas.
- Keep personal-key price ceilings, persistent chapters, retry queues and server-only credentials. Document setup in both publication READMEs.

## 0.3.9

- Clarify the existing MIT license, Pendulumgames attribution and third-party content boundaries in the public READMEs and packaged notices.

- Use the configured Valheim server name when the website DisplayName override is blank; retain the world name as a fallback.
- Document dedicated-host wildcard listeners (`http://*:PORT/`), allocated TCP ports, advertised game addresses, and troubleshooting for missing player markers.

- Pace client telemetry with separate byte allowances and a shared packet interval; pause uploads when Valheim's socket send queue is busy.
- Send one terrain tile per packet, keep at most two map batches pending, and remove immediate duplicate map sends. Retry unacknowledged events/maps/media after 30 seconds.
- Preserve portraits during upload, capture at most once per minute, and allow longer assembly time for deliberately paced high-resolution transfers.
- Log slow collector stages and network backpressure at most once per minute per stage. Dedicated-server multiplayer validation is still required for the reported Steam send-limit incident.

## 0.3.8

- Add optional host SLS zone outlines using its live bounds, levels, palette, transparency and fog setting; show zone levels above 1 on hover/tap.
- Display consented latest Nemesis scores on map tooltips and the portrait corner, using the host-observed player ZDO rather than client snapshot claims.
- Track distinct Nemesis boss deaths and team credit in filtered player metrics and leaderboards; preserve classification through retention and feed factual context to sagas.
- Keep Nemesis encounters out of ordinary progression/scenery unlocks and normal boss-speed rankings.
- Verify both SLS and Epic Loot remain optional, with independent detection and no hard assembly references.

## 0.3.7

- Expand personal and server saga evidence with career, equipment, effects, resistances, exploration, boss teams, bounties and loot context while preserving consent and narrative continuity.
- Require host OpenRouter configuration for story generation. Hide legacy template stories; leave failed generation pending instead of inventing fallback prose.
- Add encrypted personal OpenRouter keys and named model/preset choices, explicit paid opt-in, token-price ceilings and daily attempt limits.
- Unlock scenery progressively from recorded boss credit, beginning with Meadows. Public viewing and full-screen scrolling scenery are defaults for new settings; preserve existing preferences.
- Replace unexplored atlas background with a procedural textured brown parchment treatment without distributing game textures.
- Prepare a shared Thunderstore/Hexium package, original package icon, public documentation and recorded-playtest website screenshots.

## Earlier previews

Established standalone telemetry, provenance-aware loot, runtime terrain, portraits and gear icons, character/server sagas, leaderboards, personal login, responsive biome scenery and isolated Gale Test installation workflows.
