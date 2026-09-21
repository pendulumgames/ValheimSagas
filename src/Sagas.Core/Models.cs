using System;
using System.Collections.Generic;
namespace ValheimSagas;
public sealed class GemSocket { public string Prefab {get;set;}=""; public string Name {get;set;}=""; public string IconId {get;set;}=""; public List<string> Effects {get;set;}=new List<string>(); }
public sealed class SagaEvent {
 public List<GemSocket> Sockets {get;set;}=new List<GemSocket>(); public string SocketColor {get;set;}="";
 public bool NemesisBoss {get;set;}
 public double? DurationSeconds {get;set;}
 public string Id {get;set;} = ""; public string World {get;set;} = ""; public string PlayerId {get;set;} = ""; public string PlayerName {get;set;} = "";
 public DateTime Utc {get;set;} = DateTime.UtcNow; public string Kind {get;set;} = "kill"; public string Prefab {get;set;} = ""; public string Name {get;set;} = "";
 public int Stars {get;set;} public string Biome {get;set;} = ""; public bool Boss {get;set;} public int Amount {get;set;} = 1; public int Quality {get;set;} = 1;
 public string Rarity {get;set;} = ""; public string RarityColor {get;set;} = ""; public string ItemType {get;set;} = ""; public List<string> Effects {get;set;} = new List<string>(); public string Source {get;set;} = "unknown"; public string Provenance {get;set;} = "";
 public float? X {get;set;} public float? Z {get;set;} public bool DetailsExpired {get;set;} public List<string> Contributors {get;set;} = new List<string>();
}
public sealed class PlayerSnapshot { public string MapColor {get;set;}=""; public bool SharePins {get;set;} public string ProfileSlug {get;set;} = ""; public List<GearItem> Hotbar {get;set;} = new List<GearItem>(); public string PortraitStatus {get;set;} = ""; public DateTime? PositionUtc {get;set;} public bool PositionLive {get;set;} public string PortraitId {get;set;} = ""; public Dictionary<string,string> EffectiveResistances {get;set;} = new Dictionary<string,string>();
 public float? NemesisScore {get;set;}
 public bool JewelcraftingInstalled {get;set;} public bool EpicLootInstalled {get;set;} public long? Gold {get;set;}
 public bool BackgroundUnlocked {get;set;} public string BackgroundPreference {get;set;} = "automatic";
 public List<string> CompletedBossKeys {get;set;} = new List<string>();
 public int ProgressionTier {get;set;} public string ProgressionBoss {get;set;} = ""; public DateTime? LastAchievementUtc {get;set;}
 public string ProfileBiome {get;set;} = ""; public string ProfileBiomeEvidence {get;set;} = "";
 public string World {get;set;} = ""; public string PlayerId {get;set;} = ""; public string Name {get;set;} = ""; public bool Online {get;set;} public bool SharePosition {get;set;}
 public DateTime Utc {get;set;} = DateTime.UtcNow; public DateTime? LastSeenUtc {get;set;} public bool ShareMap {get;set;} public bool ShareProfile {get;set;} = true; public float? X {get;set;} public float? Z {get;set;} public List<GearItem> Gear {get;set;} = new List<GearItem>();
 public Dictionary<string,float> EffectiveStats {get;set;} = new Dictionary<string,float>(); public string StatsNote {get;set;} = "Effective values depend on skills, food, status effects and active weapon.";
}
public sealed class GearItem { public List<GemSocket> Sockets {get;set;}=new List<GemSocket>(); public string SocketColor {get;set;}=""; public int HotbarSlot {get;set;} public bool Equipped {get;set;} public bool Active {get;set;} public string IconId {get;set;} = "";
 public string Slot {get;set;} = ""; public string Name {get;set;} = ""; public string Prefab {get;set;} = ""; public string Type {get;set;} = ""; public int Quality {get;set;} = 1;
 public string Rarity {get;set;} = ""; public string RarityColor {get;set;} = ""; public float Durability {get;set;} public float MaxDurability {get;set;}
 public Dictionary<string,float> BaseStats {get;set;} = new Dictionary<string,float>(); public Dictionary<string,float> Stats {get;set;} = new Dictionary<string,float>();
 public List<string> Effects {get;set;} = new List<string>(); public string Note {get;set;} = "";
}
public sealed class MapCell { public int X {get;set;} public int Z {get;set;} public string Biome {get;set;} = "Unknown"; public float Height {get;set;} public string TerrainPixels {get;set;} = ""; }
public sealed class ExplorationBatch {
 public string World {get;set;} = ""; public string PlayerId {get;set;} = ""; public bool Imported {get;set;} public int CellSize {get;set;} = 64; public List<MapCell> Cells {get;set;} = new List<MapCell>();
}
public sealed class SagaChapter {
 public List<string> MapParticipants {get;set;} = new List<string>();
 public string Scope {get;set;} = "character"; public List<SagaParticipant> Participants {get;set;} = new List<SagaParticipant>();
 public string ServerBio {get;set;} = ""; public string ServerBioModel {get;set;} = "local-template";
 public string CharacterBio {get;set;} = ""; public string CharacterBioModel {get;set;} = "local-template";
 public string Id {get;set;} = ""; public string World {get;set;} = ""; public string PlayerId {get;set;} = ""; public DateTime Utc {get;set;} public DateTime FromUtc {get;set;} public DateTime ToUtc {get;set;}
 public string Title {get;set;} = ""; public string Text {get;set;} = ""; public string Model {get;set;} = "local-template"; public string PromptVersion {get;set;} = "1";
 public List<string> EventIds {get;set;} = new List<string>(); public List<string> Facts {get;set;} = new List<string>(); public string Summary {get;set;} = "";
}
public sealed class SagaParticipant {public string PlayerId {get;set;} = ""; public string Name {get;set;} = "";}
