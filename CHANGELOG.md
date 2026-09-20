# Changelog

## 0.3.12

- Group page layout and owner-only Viking scenery in a compact Appearance popover, with clear personal-view versus public-profile labels.
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
