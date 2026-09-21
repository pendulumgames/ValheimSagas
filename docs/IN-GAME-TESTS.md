# Required isolated runtime acceptance

Compilation and synthetic HTTP/browser tests are not evidence of game execution. Use a new test profile and new world, never existing saves/live server. Install the identical package on a host/dedicated server and two clients. Keep modded and vanilla-only runs separate.

1. **Load and transport:** check BepInEx logs for successful patching; connect two distinct characters (including same display name), inspect connection-bound IDs, names and three-second presence. Leave/rejoin, change characters and world, restart all processes. Confirm last-known gear remains offline and stale markers disappear. Verify without optional mods first, then Deep North equivalents.
2. **Combat:** spawn known 0/1/2/high-star creatures and a boss; kill with different finishing players. Count exactly one event per creature across both clients, ownership transfer and reconnect. Test fire/poison, pets, drowning, falling, disconnected attacker and player deaths. Unknown attribution is preferable to invented credit. Delete/despawn a creature: no kill. Force Lucky Loot bonus rolls: more actual dropped items, same death count. Exercise SLS asynchronous and immediate paths.
3. **Loot:** kill/drop/pickup by different characters; test partial pickup, full inventory failure, repeated drop/pickup, transfer to chest, tombstone recovery, stack split/merge, auto-stacking, AzuCraftyBoxes and other inventory mods. Confirm generated/collected totals stay distinct, provenance caps hold, uncertain merges remain unknown. Test delayed/reordered reports and reconnect retry. Inspect quality and Epic Loot sockets/effects against the game tooltip.
4. **Maps/privacy:** opt in one then both clients with different discoveries. Verify personal maps differ and union combines exactly; shared cartography does not become personal exploration. Test turning sharing off after data exists. Fetch API directly with/without token and explicit private player ID: hidden terrain must be absent. Move a public marker then turn the in-game toggle off. Check terrain orientation against known coastline/biome boundaries. Measure initial exploration-sync duration.
5. **Gear:** equip, upgrade and swap each item/slot, especially InventorySlots extensions. Check mouse/keyboard/touch tooltips and timestamps, base quality 1 versus actual quality, armor/damage/block/resistance/set/effects and evaluated effective values. Note unsupported calculations rather than summing conditional enchantments. Confirm no full inventory is exposed.
6. **Lore:** complete a configured milestone without an API key; confirm no generated chapter or template biography, while statistics remain available after restart. Opt in to an administrator-owned free key on the isolated server; confirm only free requests, correct stored model, bounded quota/retry behavior and no browser credentials. Revoke profile sharing and verify prose is hidden.
7. **History/reliability:** inject events at each filter boundary; compare server totals after restart/backup restore. Disconnect during ACK, reconnect, abruptly stop a test process and recover. Simulate denied database write/locked listener/full queue; gameplay must continue and the health/log state must show failures. Measure expected CPU/memory, network bytes and database growth with two players, many drops and a large map, then increase representative load.

Record game/mod versions, host OS, test case, expected/actual results and logs (redacting keys). Only mark an acceptance item passed after observing it. None of these multiplayer cases has been claimed as passed by the implementation run.

## 0.3.6 focused acceptance

Use Gale Test only. Synthetic browser and Unity Mono HTTP checks do not verify these game interactions.

1. Host the Test world and refresh the website with Ctrl+F5. Inspect larger equipped items and hotbar on desktop/phone, online/syncing/offline badges, full-screen text readability and the restored bottom fade.
2. On World, pan/zoom over the same kill cluster; its colors should stay consistent. Toggle sapphire loot over ember kills: overlap should blend without one replacing the other. Try collected versus dropped items and the intensity slider. Expand/collapse terrain/position/heat controls; verify their filters are independent.
3. Press Left Ctrl+F8 in game. Expect a short confirmation, with no credential in the game log. Paste into website Login within two minutes. Confirm your identity. Pressing it again revokes the previous token. Left Ctrl+F9 revokes the active token. Verify clipboard cleanup preserves an unrelated newly copied value. Customize shortcuts in Website Login if another mod uses those keys.
4. In a multiplayer Test session, a remote client must receive only their own character/world credential. Verify both host and remote issuance, old-token rejection after rotation, and no other-character edit controls. Shared viewer access remains read-only; public viewing needs no login but offers optional Login.
5. A Viking with recorded team/final-hit credit for all seven main-game bosses can choose any biome or Automatic. Confirm the choice survives refresh/restart, is visible to another viewer, and applies in both full-screen modes on Character and Saga. In 0.3.7, a partial-progress Viking can save only backgrounds through their next biome; a novice can select Meadows. Already-unrecorded historical victories cannot grant unlock; do not alter live history to fabricate progress.

Before public release, the earlier multiplayer ownership/telemetry, Linux hosting and gameplay regression items remain applicable. No public deployment has occurred.

## 0.3.7 acceptance

1. Test a new config: public viewing without a token, loopback binding, and a fresh browser defaulting to Full screen scroll. Existing private configs and saved Boxed/Full screen preferences must remain intact.
2. Verify Eikthyr credit unlocks Black Forest and Bonemass credit unlocks Mountains, including credited teammates. Log in as another Viking and verify they cannot change that preference.
3. With no host OpenRouter key, wait through a milestone: no generated saga, no template biography, no external calls. Previously generated AI chapters remain readable; legacy templates are hidden. Then configure your own host free key and verify persisted prose, ledger, actual-model provenance and cooldowns. Do not assume this credentialed step has been tested automatically.
4. Optionally use a personal login to create/select/delete a named preset, save/remove a personal key and return to Host default. Free presets must retain zero ceilings. Paid presets require explicit opt-in and positive ceilings; only perform a chargeable live request if you choose to fund it. Verify another viewer cannot read settings or keys. Restart and verify key persistence without logging secrets.
5. Inspect brown textured fog while panning/zooming and switching personal/combined maps. Previously unexplored terrain must stay masked. Check both heat layers and privacy revocation.
6. Confirm personal/server stories exclude revoked profile/map context, retain factual attribution, and do not repeat template prose. Check recorded portrait/gear remain available after player logout while host website stays running.

## 0.3.8 SLS / Nemesis

1. In Gale Test with SLS enabled, compare zone outlines/level colors with the in-game SLS map. Hover a level>1 square and a level1 square. The latter should have no level tooltip. Changing SLS palette, opacity, scaling or overlay settings should reach the website within roughly15seconds.
2. Test SLS above-fog on and off. Below fog, unknown terrain and level hover stay hidden; switch personal/combined explorers and verify the masks follow selection. Above fog, squares may cross unknown areas but terrain is still masked. Hide the optional layer in the browser.
3. Compare the player's changing Nemesis score with SLS. Inspect the map tooltip and portrait-corner badge. Log out while another host remains online; confirm last-known score and timestamp persist. Disable profile sharing and confirm score is withheld.
4. Defeat a flagged Nemesis boss with two Sagas clients; verify one event, one credit per recorded participant, correct personal time filters/leaderboard and no ordinary boss scenery unlock. Retry/reconnect/restart must not duplicate it. Ordinary bosses must not increment the Nemesis metric.
5. If lore is enabled, inspect a subsequent chapter's ledger for qualified score/Nemesis evidence. Test independent isolated profiles with SLS only, Epic Loot only and neither. Base pages, telemetry, maps and vanilla tooltips should remain usable.

Compilation, metadata, synthetic browsers and isolated Mono do not constitute these in-game checks.


## 0.3.9 Steam send-limit regression

1. Stop the dedicated server and close every participating game client before replacing plugin files with 0.3.9. Preserve config and the Sagas database; do not delete saves. Updating only the server cannot throttle older clients.
2. Restart and connect one updated client with map/profile sharing enabled. Leave the website closed for five minutes while moving, fighting and changing equipment. Watch all participants for periodic stalls and `Failed to send data k_EResultLimitExceeded` spam.
3. Open the website and verify gradual map import, named position updates, kill/loot counts, item icons and eventual portrait completion. Open the website from a second browser; confirm this does not reintroduce gameplay stalls.
4. Repeat with two updated clients joining together and sharing existing exploration/high-resolution portraits. Record whether all clients remain responsive. A congested connection may log `Sagas uploads paused`; it should resume without losing pending events when the queue drains.
5. Disconnect/reconnect one client and verify deduplicated kills/loot, durable event retry and portrait completion. Confirm privacy switches stop queued map/art uploads.
6. If stalls or send-limit spam continue, stop the test and compare with Sagas disabled on clients and host. Supply client BepInEx logs and the host console log covering the same interval, identifying versions and which process emitted each message. Redact keys/tokens. Do not keep a congested live session running to gather more spam.


## 0.3.10 host-funded preset acceptance

Use an operator-controlled OpenRouter key/preset with chosen monetary controls. Set Model to `@preset/your-saga-preset`, AllowPaidModels=true and a small DailyBudget; restart the host. Trigger consenting adventures for two players using Host default and verify generated personal chapters plus a server chapter, actual returned model provenance and shared daily usage. Change preset model/provider/pricing in OpenRouter and check that subsequent requests follow it; existing chapters must remain unchanged. Disable AllowPaidModels and confirm a paid-only preset cannot charge (generation remains pending if no free route qualifies). No operator credential or paid call is part of the automated checks.

## 0.3.13 periodic-stall retest

Launch Gale Test and confirm `Loading [Valheim Sagas 0.3.13]`. Play for at least 10 minutes, move into unexplored terrain, swap hotbar/gear and check the website's map, SLS colors/levels, Nemesis score and item rarity/effects. Include a period with the website closed. Compare felt three-second hitches and logs against the September 20 local 0.3.10 baseline: 89 slow world-update reports (168-434 ms) and recurring SLS/equipment reports, without Steam send-limit errors.

Inspect `Sagas slow stage: world update`, `equipment snapshot`, `SLS capture slice`, `portrait capture`, `Sagas uploads paused` and `k_EResultLimitExceeded`. Timing warnings are sampled at most once per minute per stage, not a complete frame-time profile. A rare portrait capture warning is separate from recurring three-second work. Verify profile images still refresh after equipment changes.

Repeat with host and clients upgraded on the dedicated server before declaring the original multiplayer incident resolved. Synthetic slice tests and an isolated installed-Mono probe are not an in-game performance result.

## 0.3.14 portrait retest

Confirm 0.3.14 in the startup log. Stand/play with unchanged gear for several minutes: there should be no minute-by-minute portrait renders. Change weapon, shield, armor, hair or cosmetics; after the outfit settles (and any capture/upload cooldown), verify a new correctly oriented portrait with body, transparency and weapon effects. Old imagery should remain visible during processing. An unchanged character has a ten-minute fallback refresh.

Compare `Sagas portrait timings` preparation, visibility/framing, final render/readback and background matte/encode values with the previous approximately 223 ms combined capture. Background timings do not block the Unity update. Check rapid swaps and disabling profile sharing during processing: an old job must not publish a stale or private portrait. Check logout/rejoin and world changes. No asynchronous GPU readback or frame-spread rendering is used.


## 0.3.15 staged portrait acceptance

1. Run Gale Test and host the isolated test world. Open the website; check the full body, upright orientation, feet/framing, neutral lighting and equipped weapon effects after capture finishes. A short delay is expected while preparation, readback and upload complete.
2. Leave appearance unchanged for at least ten minutes. No periodic portrait capture should start. Swap weapons several times rapidly, then settle; expect a replacement after debounce and at least ten seconds between capture starts. Compare `Sagas staged portrait` maximum main-thread work/frame with the previous 48–110 ms synchronous captures. Background encode time is not a main-thread stall measurement.
3. Change gear while seated on the ground or in a chair. Keep the saved portrait until standing and settled. Repeat on a fresh profile without a saved image (placeholder until eligible), and while attached to a bed. A seated pose must not be published.
4. During preparation, change gear again, log out/change worlds, or revoke profile sharing. No outdated image should be published. Restore sharing and verify recovery; revocation must still deny media access. Confirm saved artwork survives logout and host restart.
5. On a graphics backend without asynchronous readback, expect `async-readback-unavailable` and retained artwork, not a blocking fallback. Capture failures retry no faster than once per minute. Report graphics backend and complete logs if images are empty, inverted or stuck.
6. Retest on a dedicated host with two clients: each client generates its own image; the server persists and serves it. Monitor frame timings and network backpressure independently. Automated checks do not establish hitch-free gameplay or multiplayer performance.


## 0.3.16 startup acceptance

1. Restart Gale Test fully, host the isolated world and compare startup logs against the previous 334 ms world update, 106 ms equipment and 231 ms exploration samples. Look for background service startup (worker time), atlas render/setup, atlas callback maximum, icon queue maximum and portrait phase costs. Worker elapsed time is not a frame stall; individual render operations still require real measurements.
2. Confirm the website becomes available, the live player appears, initial events eventually arrive once, and equipment icons populate after the queue drains. Check current gear matches the latest rapid swaps, including switching before icons finish.
3. Verify map orientation/colors and fog boundaries match the in-game map. A coherent atlas must appear after preparation; no transient blocky fallback tiles should be exported while it is pending. Explore more terrain and verify ongoing updates.
4. Leave/change worlds during startup/map preparation, then immediately reconnect. No old-world atlas, stale service or locked database should be adopted. Close and reopen the host and confirm queued events and saved media survive. Revoke sharing during preparation and verify no private media/map export.
5. Repeat on the dedicated host with two clients. Host initialization should run off-thread; graphical map, icon and portrait work remains on clients. Test unsupported/failed graphics readback separately where available; degraded terrain is explicitly logged. No dedicated acceptance is claimed by automated tests.


## 0.3.17 startup attribution and subtle zones

Fully restart Gale Test. Compare initial world/equipment warnings with143.4/88.2ms from0.3.16 and review `Sagas startup detail` phase breakdowns. Confirm Epic Loot rarity/effects, SLS zones and Nemesis still work. First-use JIT before method entry is not in the phase totals. Hard-refresh the website: zone outlines should be subtle20% maximum, lower host opacity still honored, identical boundaries no darker, and fog/hover unchanged. Do not change the in-game SLS opacity for this test.
