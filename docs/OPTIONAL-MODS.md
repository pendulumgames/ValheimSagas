# Optional integrations

Sagas has soft BepInEx dependencies on `MidnightsFX.StarLevelSystem` and `randyknapp.mods.epicloot`; it has no assembly reference to either mod. Its Core and website do not load their DLLs. The same Sagas package works without both. All participating game clients still need the matching Sagas release for full telemetry.

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
