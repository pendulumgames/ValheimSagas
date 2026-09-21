# Valheim Sagas

**Every Viking has a story.**

Your adventures, remembered: a standalone Valheim mod with a server-hosted website, a living 2D atlas, character armories, leaderboards and optional AI-written sagas.

![Viking profile with saved portrait, equipment, Nemesis score and compact Appearance control](https://raw.githubusercontent.com/pendulumgames/ValheimSagas/main/docs/screenshots/character.png)

## Explore your fellowship

- **World Atlas:** Valheim terrain captured at runtime, textured brown fog of war, personal or combined exploration, named player markers, online/offline filters and separate kill/loot heat maps.
- **Vikings:** persisted equipped-character portraits, game item icons, detailed hover/focus/tap tooltips, resistances, gear effects and personal statistics. Shared portraits and equipment remain available after logout while the host website is running.
- **Leaderboards:** boss progression and credited teams, observed fight times, kills and stars, rarity discoveries, carried gold and optional Epic Loot bounties.
- **Optional SLS enhancements:** host-colored zone squares that follow SLS fog settings, hover levels above 1, current Nemesis scores and distinct Nemesis boss-kill rankings.
- **Sagas:** persistent personal and server stories with a separate recorded-facts ledger. The host must configure OpenRouter first; no template stories are generated.
- **Make it yours:** boxed, full-screen or full-screen scrolling scenery. Your personal login can select backgrounds up to your recorded boss progression; everyone sees the saved selection. New browsers default to full-screen scroll.

![World Atlas with SLS zone boundaries, a last-known Viking marker and Nemesis tooltip](https://raw.githubusercontent.com/pendulumgames/ValheimSagas/main/docs/screenshots/atlas.png)

Statistics support all history, 30 minutes, 1/6/12 hours, 1/3/7 days and custom dates. Live presence and last-known equipment are independent of the selected time window. Drops, verified earned collections and unknown pickups remain distinct; repeat pickups and bonus loot rolls cannot create extra kills.

![Server rankings including optional Nemesis boss kills](https://raw.githubusercontent.com/pendulumgames/ValheimSagas/main/docs/screenshots/leaderboard.png)

Screenshots show an isolated copy of recorded playtest data, with display names replaced for publication. The atlas includes a last-known player marker and the host's SLS boundaries clipped by exploration. The Nemesis leaderboard is shown honestly with no recorded victories yet. These are website captures, not evidence that every multiplayer integration has been verified.

## Install and open

1. Install BepInEx 5 and **Valheim Sagas on the host and every participating client**. Use the same release on each. In a mod manager, install the package; manually, extract its `BepInEx` folder into your profile/game installation. Keep the four plugin DLLs and `web` folder together.
2. Start a world. A dedicated server is optional: a normal hosted or solo world starts the website on that computer. The website stops when the host closes the world/game.
3. On the host computer, open **http://127.0.0.1:8877/**. New configurations allow public viewing without a viewer token. This does not expose a public internet port: the default binding remains loopback.
4. Settings are in `BepInEx/config/org.valheimsagas.collector.cfg`. Existing saved settings are preserved on upgrade. Restart the host after changing server settings.
5. For a dedicated host, use its allocated website TCP port and the wildcard listener described below. Visitors open `http://SERVER-IP:PORT/`. An HTTPS reverse proxy is optional for a domain and encrypted access; no separate website installation is required.

All participating players need Sagas for full telemetry, portraits and exploration. Epic Loot and Star Level System are optional. Game/Unity assemblies and runtime game assets are not bundled. Biome backgrounds are bundled as responsive WebP images for offline use; unused original PNG artwork is excluded from releases.

## Dedicated servers: IP and allocated port

Stop the server and edit the existing `[Server]` entries in `BepInEx/config/org.valheimsagas.collector.cfg`. For a hosting panel that allocates port **19908**:

```ini
[Server]
EnableWebsite = true
ListenPrefix = http://*:19908/
RequireViewerToken = false
DisplayName =
AdvertisedAddress =
```

Restart the server, then open **`http://YOUR-SERVER-IP:19908/`**. Replace `19908` with your allocated website port. Keep the `http://` and trailing `/`; the wildcard `*` belongs in the configuration, not the browser address. `127.0.0.1` only accepts local requests and will not make the website accessible to remote players. The host must allow **TCP** on the chosen port; keep the Valheim game/query ports unchanged. You do not need to create a panel reverse proxy for direct IP access.

With `DisplayName` blank, 0.3.9 reads the host's configured Valheim server name, falling back to its world name. Set `DisplayName = Your Fellowship` to override it. Set `AdvertisedAddress = YOUR-SERVER-IP:GAME-PORT` if you want the website to display the address players use to join Valheim. This display-only field does not configure the website listener; the game and website ports are usually different. Neither your public IP nor a panel hostname is discovered automatically. Restart after changing these settings.

Public viewing is enabled by `RequireViewerToken = false`; existing configurations are preserved on upgrade. For personal login and OpenRouter key submission over the internet, use an HTTPS reverse proxy so credentials are encrypted in transit.

### Missing players or stale map positions

A congested game connection can delay player snapshots, exploration and portraits even while the website itself opens normally. Upgrade the host **and every Sagas client** to 0.3.9 or later and restart their connections. A server name comes from host configuration, so a wrong name is a separate issue from delayed player uploads. If all website data is blank, check the website/API error and server logs too.

For a live marker, the player needs Sagas installed, `[Privacy] SharePosition = true`, and **Visible to other players** enabled on Valheim's in-game map. The website's **Online Vikings** layer must be enabled and its player selection must include that player. Offline positions require the separate **Offline - last known** layer. Terrain additionally requires `ShareMap = true`. Upload pacing cannot override these privacy settings.

## Multiplayer upload pacing (0.3.9 and later)

Upgrade **both the host and every participating client** to the same current release, then restart their Valheim processes. Version 0.3.9 addresses a reported three-second multiplayer stall with Steam `k_EResultLimitExceeded` log spam: background transfers now yield to the game send queue, use separate byte allowances and wait 30 seconds before retrying unacknowledged data. Older clients still send bursts, so updating only the host is insufficient.

Maps import gradually and a first high-resolution portrait can take several minutes on a busy connection. Existing saved portraits remain visible while a replacement uploads. The collector logs `Sagas uploads paused` when the game queue is busy and `Sagas slow stage` for work exceeding 50 ms, with repeated warnings limited to once per minute. These diagnostics contain timings/queue sizes, not credentials or coordinates. The multiplayer incident still requires an operator retest; automated transport checks are not in-game verification.

## Privacy and personal login

`[Server] RequireViewerToken = false` is the public-viewing default. Set it to `true` to require the shared `ViewerToken` for viewing. Public viewers still only receive data permitted by each player's sharing settings.

Portraits update after equipment or appearance changes settle, with a two-second debounce and a ten-second minimum capture cooldown. Unchanged appearances do not trigger periodic captures. Sitting or attached poses defer a new capture until you stand and settle; the last shared portrait remains visible. Preparation spans multiple frames, GPU readback is asynchronous, and image processing runs in the background. Portraits require a client graphics device supporting asynchronous readback; dedicated servers receive the images from clients. Initial host storage/website startup runs in the background. Map pixels arrive through bounded asynchronous strips, and uncached item icons are queued one per frame; the website and artwork may take slightly longer to become ready. The `Sagas staged portrait` log reports the maximum main-thread capture work in a frame. Actual rendering still runs on the game thread, so playtesting is required to measure the remaining frame cost.

Map sharing defaults on for new configurations. Profile, portrait and position sharing remain configurable. Live position also follows Valheim's **Visible to other players** map setting. The atlas only receives permitted exploration; fog is not a client-side substitute for access control.

The header shows **Login** when signed out. After login, it shows your saved Viking portrait (or an initial when no portrait is available). Click the icon for **Saga Settings** or **Log out**. Shared server tokens show a read-only account; Saga Settings explains how to switch to a personal login.

While playing, press **Left Ctrl + F8** to copy a personal login token, then paste it into the website's **Login** dialog. This replaces the previous token for that character/world. **Left Ctrl + F9** revokes it. A personal login enables owner-only scenery and storyteller settings; the shared viewer token does not grant ownership. Tokens are stored in the browser tab session and sent in Authorization headers, never in profile URLs.

Scenery progression follows recorded credit: Meadows initially; Eikthyr unlocks Black Forest, Elder unlocks Swamp, Bonemass unlocks Mountains, Moder unlocks Plains, Yagluth unlocks Mistlands, Queen unlocks Ashlands, and Fader unlocks Deep North. Tracking cannot reconstruct unrecorded boss kills from before installation.

## Optional SLS and Epic Loot

Neither mod is required. Without them, statistics, vanilla gear/tooltips, portraits, maps, leaderboards and sagas continue to work. SLS and Epic Loot are independently detected; installing one does not require the other.

When **SLS is installed on the host**, the atlas mirrors its enabled zone overlay, resolved level palette, transparency and above/below-fog setting. The website adds a zone-level tooltip only above level 1. Below-fog outlines and tooltips follow the selected shared exploration; above-fog mode reveals the zone grid without revealing terrain. Host zone settings refresh about every 15 seconds. The website also offers a local SLS-layer visibility toggle.

With the SLS Nemesis system enabled, a received score appears on the Viking's portrait and map tooltip. It is the latest host-observed replicated score, not a lifetime total; missing scores remain unknown. Profile privacy still applies. Nemesis boss defeats have separate filtered player metrics and a team-credit leaderboard, and can inform saga evidence. Repeated death reports count once. These encounters do not unlock ordinary boss progression/scenery or enter normal boss-speed rankings. Older events lacking the Nemesis flag cannot safely be backfilled.

Epic Loot independently enhances rarity colors, gear effects, bounty tracking and loot presentation. It is never required for SLS features or basic website operation. See [integration details](https://github.com/pendulumgames/ValheimSagas/blob/main/docs/OPTIONAL-MODS.md).

## Enable sagas

On the **host**, set `[Lore] OpenRouterKey` or the host process's `OPENROUTER_API_KEY` environment variable. `EnableOpenRouter` must be true. An OpenRouter account/key is required, even for free routing; Sagas never accesses an existing browser account automatically.

The host default is `openrouter/free`, with `AllowPaidModels = false`, a 20-attempt daily budget and no paid fallback. Without a host key, stats, maps, portraits and leaderboards work normally; saga prose waits. Old template chapters are hidden and preserved locally rather than displayed as generated stories.

### Host-funded models and OpenRouter presets

In 0.3.10 or later, the host can pay for everyone's default Viking sagas and the server saga using one OpenRouter key. Configure the host's `[Lore]` section, then restart:

```ini
[Lore]
EnableOpenRouter = true
OpenRouterKey = YOUR_OPENROUTER_KEY
Model = @preset/your-saga-preset
AllowPaidModels = true
DailyBudget = 20
```

Create the preset in the OpenRouter account that owns the key. **Manage model selection, provider routing and price limits in that OpenRouter preset.** Sagas does not send a provider/pricing override for paid host requests. There is no second host price ceiling to configure in Sagas. If the preset has no price restriction, Sagas does not add one. A direct model ID also works (for example `deepseek/deepseek-v4-flash-0731`); its normal OpenRouter/account pricing applies.

`DailyBudget` limits HTTP attempts per UTC day, including retries, shared across host-funded Viking and server sagas. It is a request count, not a dollar budget. Set monetary controls in OpenRouter. Sagas still controls the factual narrative prompt, structured output request, maximum output length and disabled tools/plugins; a preset does not replace those application requirements. Existing chapters remain cached and are not rewritten when the model changes.

New installations keep `Model = openrouter/free` and `AllowPaidModels = false`. With paid routing disabled, Sagas rejects paid direct models and imposes zero token-price limits on presets. Players can keep **Host default** without adding their own keys; personal paid overrides retain their separate personal key and explicit limits.

After signing in, click your **profile icon -> Saga Settings**. Choose **Server storyteller** to use the host's setup, or **My own storyteller** to enter your own OpenRouter key and model/preset. Create up to eight **Saga Profiles**, each with its own saved OpenRouter key and model/preset. Select a profile and save to switch both together. Request limits and profile/key removal are under **Advanced options**. A preset can select a model or an OpenRouter `@preset/name`. Paid routing requires the player's explicit checkbox, a per-million-token price ceiling and a daily attempt limit. The host key must still be configured. Personal keys are encrypted on the host and never returned to viewers, but the host administrator controls that machine: only entrust a key to a host you trust. HTTPS is required for remote browser key submission. Host-paid routing is described above.

Story prompts use relevant recorded events, career totals, bosses and credited teammates, notable loot, current equipment/effects, resistances, bounties and shared exploration summaries. Context is bounded and respects sharing consent; it does not transmit raw account/world IDs, precise coordinates or API keys as story content. Fiction is labeled separately from facts. See [lore details](https://github.com/pendulumgames/ValheimSagas/blob/main/docs/LORE.md).

## Data and release status

Back up `BepInEx/config/ValheimSagas` with the host stopped, including `sagas.db` and `personal-lore.key` if present. The key file is required to decrypt saved personal OpenRouter credentials. Do not share backups, configs or tokens publicly. Uninstalling the plugin does not delete recorded history.

**0.3.16 is a preview release verified with automated checks.** It includes performance fixes for SLS/gear snapshots and staged, change-triggered portrait capture with asynchronous GPU readback and background image processing; fresh local and dedicated-server playtests must confirm frame-time improvements. Automated tests use labeled synthetic fixtures; real credentialed OpenRouter generation, two-client multiplayer acceptance and Linux hosting still require testing. Unresolved attackers and uncertain loot provenance stay unattributed rather than being guessed. Fight durations are observed telemetry, and carried gold is a snapshot, not a lifetime earnings counter.

## License

Valheim Sagas is released under the [MIT License](https://github.com/pendulumgames/ValheimSagas/blob/main/LICENSE), copyright 2026 Pendulumgames and Valheim Sagas contributors. The license is included in every distribution.

Third-party dependencies retain their own copyrights and license terms; see [third-party notices](https://github.com/pendulumgames/ValheimSagas/blob/main/THIRD-PARTY-NOTICES.md). The Sagas license does not grant rights to Valheim assets or other third-party content shown in runtime captures or screenshots. Valheim belongs to Iron Gate AB. This is an independent community mod.

**Appearance:** On a Viking page, open Appearance to choose Boxed, Full Screen, or Full Screen Scroll for your own browser. When viewing your own Viking while logged in, the same popover lets you save unlocked scenery visible to everyone.
