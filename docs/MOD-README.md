# Valheim Sagas

**Every Viking has a story.**

Your adventures, remembered: a standalone Valheim mod with a server-hosted website, a living 2D atlas, character armories, leaderboards and optional AI-written sagas. No ValheimWebMap dependency.

![Character armory with equipped Viking and biome scenery](https://raw.githubusercontent.com/pendulumgames/ValheimSagas/main/docs/screenshots/character.png)

## Explore your fellowship

- **World Atlas:** Valheim terrain captured at runtime, textured brown fog of war, personal or combined exploration, named player markers, online/offline filters and separate kill/loot heat maps.
- **Vikings:** persisted equipped-character portraits, game item icons, detailed hover/focus/tap tooltips, resistances, gear effects and personal statistics. Shared portraits and equipment remain available after logout while the host website is running.
- **Leaderboards:** boss progression and credited teams, observed fight times, kills and stars, rarity discoveries, carried gold and optional Epic Loot bounties.
- **Optional SLS enhancements:** host-colored zone squares that follow SLS fog settings, hover levels above 1, current Nemesis scores and distinct Nemesis boss-kill rankings.
- **Sagas:** persistent personal and server stories with a separate recorded-facts ledger. The host must configure OpenRouter first; no template stories are generated.
- **Make it yours:** boxed, full-screen or full-screen scrolling scenery. Your personal login can select backgrounds up to your recorded boss progression; everyone sees the saved selection. New browsers default to full-screen scroll.

![World Atlas, exploration and activity](https://raw.githubusercontent.com/pendulumgames/ValheimSagas/main/docs/screenshots/atlas.png)

Statistics support all history, 30 minutes, 1/6/12 hours, 1/3/7 days and custom dates. Live presence and last-known equipment are independent of the selected time window. Drops, verified earned collections and unknown pickups remain distinct; repeat pickups and bonus loot rolls cannot create extra kills.

![Server leaderboards](https://raw.githubusercontent.com/pendulumgames/ValheimSagas/main/docs/screenshots/leaderboard.png)

Screenshots show an isolated copy of recorded playtest data, with display names replaced for publication. They are website captures, not evidence that every multiplayer integration has been verified.

## Install and open

1. Install BepInEx 5 and **Valheim Sagas on the host and every participating client**. Use the same release on each. In a mod manager, install the package; manually, extract its `BepInEx` folder into your profile/game installation. Keep the four plugin DLLs and `web` folder together.
2. Start a world. A dedicated server is optional: a normal hosted or solo world starts the website on that computer. The website stops when the host closes the world/game.
3. On the host computer, open **http://127.0.0.1:8877/**. New configurations allow public viewing without a viewer token. This does not expose a public internet port: the default binding remains loopback.
4. Settings are in `BepInEx/config/org.valheimsagas.collector.cfg`. Existing saved settings are preserved on upgrade. Restart the host after changing server settings.
5. For remote website access, configure an HTTPS reverse proxy to the loopback service. Remote access is a host setup step; installation does not configure your firewall or publish the site.

All participating players need Sagas for full telemetry, portraits and exploration. Epic Loot and Star Level System are optional. Game/Unity assemblies and runtime game assets are not bundled.

## Privacy and personal login

`[Server] RequireViewerToken = false` is the public-viewing default. Set it to `true` to require the shared `ViewerToken` for viewing. Public viewers still only receive data permitted by each player's sharing settings.

Map sharing defaults on for new configurations. Profile, portrait and position sharing remain configurable. Live position also follows Valheim's **Visible to other players** map setting. The atlas only receives permitted exploration; fog is not a client-side substitute for access control.

While playing, press **Left Ctrl + F8** to copy a personal login token, then paste it into the website's **Login** dialog. This replaces the previous token for that character/world. **Left Ctrl + F9** revokes it. A personal login enables owner-only scenery and storyteller settings; the shared viewer token does not grant ownership. Tokens are stored in the browser tab session and sent in Authorization headers, never in profile URLs.

Scenery progression follows recorded credit: Meadows initially; Eikthyr unlocks Black Forest, Elder unlocks Swamp, Bonemass unlocks Mountains, Moder unlocks Plains, Yagluth unlocks Mistlands, Queen unlocks Ashlands, and Fader unlocks Deep North. Tracking cannot reconstruct unrecorded boss kills from before installation.

## Optional SLS and Epic Loot

Neither mod is required. Without them, statistics, vanilla gear/tooltips, portraits, maps, leaderboards and sagas continue to work. SLS and Epic Loot are independently detected; installing one does not require the other.

When **SLS is installed on the host**, the atlas mirrors its enabled zone overlay, resolved level palette, transparency and above/below-fog setting. The website adds a zone-level tooltip only above level 1. Below-fog outlines and tooltips follow the selected shared exploration; above-fog mode reveals the zone grid without revealing terrain. Host zone settings refresh about every 15 seconds. The website also offers a local SLS-layer visibility toggle.

With the SLS Nemesis system enabled, a received score appears on the Viking's portrait and map tooltip. It is the latest host-observed replicated score, not a lifetime total; missing scores remain unknown. Profile privacy still applies. Nemesis boss defeats have separate filtered player metrics and a team-credit leaderboard, and can inform saga evidence. Repeated death reports count once. These encounters do not unlock ordinary boss progression/scenery or enter normal boss-speed rankings. Older events lacking the Nemesis flag cannot safely be backfilled.

Epic Loot independently enhances rarity colors, gear effects, bounty tracking and loot presentation. It is never required for SLS features or basic website operation. See [integration details](https://github.com/pendulumgames/ValheimSagas/blob/main/docs/OPTIONAL-MODS.md).

## Enable sagas

On the **host**, set `[Lore] OpenRouterKey` or the host process's `OPENROUTER_API_KEY` environment variable. `EnableOpenRouter` must be true. An OpenRouter account/key is required, even for free routing; Sagas never accesses an existing browser account automatically.

The host default is `openrouter/free`, with zero token-price ceilings, a 20-attempt daily budget and no paid fallback. Without a host key, stats, maps, portraits and leaderboards work normally; saga prose waits. Old template chapters are hidden and preserved locally rather than displayed as generated stories.

Signed-in players may optionally save their own OpenRouter key and up to eight named storyteller presets in **Login**. A preset can select a model or an OpenRouter `@preset/name`. Paid routing requires the player's explicit checkbox, a per-million-token price ceiling and a daily attempt limit. The host key must still be configured. Personal keys are encrypted on the host and never returned to viewers, but the host administrator controls that machine: only entrust a key to a host you trust. HTTPS is required for remote browser key submission. Host generation always remains free-only.

Story prompts use relevant recorded events, career totals, bosses and credited teammates, notable loot, current equipment/effects, resistances, bounties and shared exploration summaries. Context is bounded and respects sharing consent; it does not transmit raw account/world IDs, precise coordinates or API keys as story content. Fiction is labeled separately from facts. See [lore details](https://github.com/pendulumgames/ValheimSagas/blob/main/docs/LORE.md).

## Data and release status

Back up `BepInEx/config/ValheimSagas` with the host stopped, including `sagas.db` and `personal-lore.key` if present. The key file is required to decrypt saved personal OpenRouter credentials. Do not share backups, configs or tokens publicly. Uninstalling the plugin does not delete recorded history.

**0.3.8 is a tested preview release.** Automated tests use labeled synthetic fixtures; real credentialed OpenRouter generation, two-client multiplayer acceptance and Linux hosting still require testing. Unresolved attackers and uncertain loot provenance stay unattributed rather than being guessed. Fight durations are observed telemetry, and carried gold is a snapshot, not a lifetime earnings counter.

Licensed under MIT; see the included third-party notices. Valheim belongs to Iron Gate AB. This is an independent community mod.
