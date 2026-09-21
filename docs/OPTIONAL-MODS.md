# Optional integrations

Sagas has soft BepInEx dependencies on `MidnightsFX.StarLevelSystem` `randyknapp.mods.epicloot` and `org.bepinex.plugins.jewelcrafting`; it has no assembly reference to these mods. Its Core and website do not load their DLLs. The same Sagas package works without any of them. All participating game clients still need the matching Sagas release for full telemetry.

## Star Level System

Read-only inspection and metadata checks cover SLS 1.15.0 in Gale Test and 1.14.0 in the original Deep North profile. These checks do not execute game hooks. No SLS files/configuration were modified or bundled.

The host samples SLS state on Unity's main thread every 15 seconds: `ZoneScaleSystemData.Zones` bounds/levels, `Colorization.zoneOverlayColors`, and ValConfig's `EnableZoneScalingBonus`, `EnableZoneMapOverlay`, `ZoneOverlayAboveFog`, `ZoneOverlayColorTransparency`. Colors cycle exactly by level, using the resolved SLS palette. Square outlines use one map-pixel inset (12m fallback on a dedicated host without Minimap), not colored terrain fills. The validated snapshot persists per world for website reads; no game API is called from an HTTP worker. More than 40,000 zones fails safely with a diagnostic instead of truncating invisibly.

Below fog, the server omits zones with no permitted explored sample. The browser clips the remaining outlines against the selected map's exploration alpha and only shows level tooltips at known points. A partly known zone's level/bounds may appear in its metadata; no unknown zone's terrain is sent. Above-fog mode intentionally exposes configured zone bounds/levels, as SLS requests, while terrain remains fogged. A zone tooltip appears only for level greater than1. Turning off zone scaling or its SLS overlay hides the website layer; the website checkbox can further hide it locally.

Nemesis scores use the player's replicated ZDO float `SLS_NEM_SCORE`. The host resolves the authenticated peer's character ZDO or its local player; it ignores any score supplied in a Sagas snapshot. No received value means unknown, not zero or an invented neutral score. SLS calculates and replicates this value, so this is source/identity checking, not an anticheat system. Offline profiles retain the last recorded value with their snapshot timestamp. Profile sharing and host Nemesis availability govern display; an absent mod hides previously persisted optional UI data.

The existing owning-client death capture samples `SLS_NEM_BOSS` before destruction. The same stable event ID and durable deduplication apply, including ownership/retry behavior. Metrics credit each recorded consenting finisher/contributor once per encounter. We deliberately do not use SLS's local `BossKillsHistory`, which also counts ordinary nearby bosses and resets periodically. Nemesis kills do not count as ordinary boss progression, scenery receipts or normal boss speed records, even if they use a vanilla boss prefab. Generic creature kill counts still include them.

Score and separate Nemesis kill evidence can enrich sagas; a snapshot score is never treated as a lifetime total. Detail retention preserves boss classification/contributors. Older Sagas releases did not record the flag, so old Nemesis kills/progression receipts cannot be reliably reclassified. Use matched Sagas versions on all players for new telemetry.

## Epic Loot

Rarity/effects/color and bounty integrations use optional reflection and guarded patches. Missing Epic Loot leaves ordinary item stats/icons, map, profiles and core tracking functional. Optional metadata failures do not invent rarity or bounty data. SLS does not depend on this integration. Neither optional DLL, their assets nor their configuration is included in the package.

## Validation and next runtime checks

Synthetic tests exercise both features present/absent independently, zone privacy/alpha masks, API persistence, score redaction, team deduplication, time filters, retention, lore and desktop/mobile UI. Assembly metadata checks verify exact inspected APIs and both soft dependencies. The installed Unity Mono probe tests the Core/API path with synthetic data, not the game collector itself.

For live acceptance: compare the same SLS square in game and website, switch its above-fog setting and palette/opacity, inspect another selected Viking's exploration, compare a changing score, defeat one flagged Nemesis boss with two players, restart and verify one encounter/two credits, then test an isolated profile without either optional mod. Do not alter an existing world or fabricate historical kills to test progression.

## Jewelcrafting 2.0.10

Read-only inspection uses the installed Gale Jewelcrafting profile. The adapter caches types/methods inside the registered plugin assembly; no global type scans, hard assembly reference or new polling job. `API.GetSocketableItemColor(ItemData)` gates `API.GetGems(ItemData)` to equipment `Sockets`, excluding `SocketBag`/fusion boxes. Ordered null slots remain empty/unavailable. `GemInfo.gemPrefab` and `gemEffectsPowerRange` supply names and configured ranges; `gemEffects` midpoint and `gemSeed` are deliberately not exposed as rolled totals. Bounds are 11 socket records and 8 effect descriptions per socket, matching the installed internal socket limit. Unsupported variants log a rate-limited warning and retain ordinary item tracking.

`API.GetEquippedJewelry(Player)` plus `Visual.equippedFingerItem/equippedNeckItem` adds extra equipped slots without duplicate inventory items. `JewelrySetup.upgradeableJewelry` gates armor values from installed ItemData.GetArmor APIs. Portrait appearance signatures include those equipped items, using the existing change-driven debounce/cooldown/capture pipeline. Gem artwork uses the existing one-uncached-icon-per-frame queue and shared upload budget. No game textures are bundled. HTTP gem artwork requires an owned, currently shared gear/hotbar reference and remains available for last-known offline snapshots.

`Utils.DropPrefabItem(GameObject, Character)` returns actual equipment spawns outside vanilla DropItems. A guarded optional postfix marks only owned, dead non-player creature spawns; the existing ItemDrop.Start stage records sockets after SpawnEquipment has saved them. Normal JC gem drops are added to the ordinary creature list and already use the existing hook. Never count GenerateDropList previews as drops. Crafting/chest/container contents and unknown-origin pickups are not promoted into earned collections. Exact gameplay ordering and multiplayer ownership remain playtest requirements.

Socket metadata persists separately from Epic Loot rarity/effects, follows provenance reconciliation, and expires with detailed effect history. Heat-map entries distinguish socket loadouts. Lore uses bounded shared snapshot/event gem facts without claiming configured powers as observed combat performance. Rarity leaderboards remain rarity leaderboards; there is no inferred JC rarity, crafting/synergy ranking, or gem-bag inventory exposure.

Validation: actual installed signature/field checks; production adapter compiled against synthetic API contracts in tests/Sagas.JewelChecks with present/absent runs; Core tests for privacy, persistence/restart, socket-only media, validation bounds, duplicate drop/collection reconciliation and saga context; browser tests for both/one/no item integration, gem image decode, range labels, empty slots, keyboard/tap/hotbar, colors, escaping and responsive layout. These are not in-game Jewelcrafting tests.

### Mod-aware statistics (0.3.25)

Epic Loot rarity and bounty panels depend on the selected world's latest shared profiles reporting Epic Loot. Jewelcrafting reports a separate installed flag and adds filled equipped-gem counts and total equipped-socket capacity. These are last-known loadout snapshots, not time-filtered acquisitions, crafting history, synergy or combat power. Main equipment is counted once; hotbar duplicates and container contents are excluded. SLS Nemesis panels retain host-installed/enabled gating. Private profiles do not contribute flags or rankings. No integration is required for ordinary map, kill, loot, gold or boss statistics.
