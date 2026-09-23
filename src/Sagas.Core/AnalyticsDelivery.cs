using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace ValheimSagas;
public sealed partial class SagaStore {
 // Only analytics consumers use these snapshots, read-only. Public Events keeps its independent-copy contract.
 // Two worlds / 100k events bound retained memory. Larger worlds still return complete, uncached history.
 readonly Dictionary<string,IReadOnlyList<SagaEvent>> analyticsHistory=new Dictionary<string,IReadOnlyList<SagaEvent>>(StringComparer.Ordinal);
 readonly Dictionary<string,List<SagaEvent>> analyticsPending=new Dictionary<string,List<SagaEvent>>(StringComparer.Ordinal);
 HashSet<string>? indexedWorlds;
 void EnsureWorldIndex(){if(indexedWorlds==null)indexedWorlds=new HashSet<string>(db.GetCollection("players").FindAll().Select(d=>d["world"].AsString).Concat(db.GetCollection("events").FindAll().Select(d=>d["world"].AsString)),StringComparer.Ordinal);}
 void RememberWorld(string world){if(indexedWorlds!=null)indexedWorlds.Add(world);}
 void InvalidateAnalytics(string? world=null){if(world==null){analyticsHistory.Clear();analyticsPending.Clear();}else{analyticsHistory.Remove(world);analyticsPending.Remove(world);RememberWorld(world);}}
 void AppendAnalytics(string world,IEnumerable<SagaEvent> added){RememberWorld(world);if(!analyticsHistory.ContainsKey(world))return;if(!analyticsPending.TryGetValue(world,out var pending))analyticsPending[world]=pending=new List<SagaEvent>();foreach(var e in added)pending.Add(JsonConvert.DeserializeObject<SagaEvent>(JsonConvert.SerializeObject(e))!);if(pending.Count>10000)InvalidateAnalytics(world);}


 internal void HideUnknownEventCoordinates(string world,HashSet<string> owners,IEnumerable<SagaEvent> events){
  // Request-local tile masks eliminate repeated database reads for clustered encounters.
  var masks=new Dictionary<(int,int),byte[]>();
  foreach(var e in events){if(!owners.Contains(e.PlayerId)||!e.X.HasValue||!e.Z.HasValue){e.X=null;e.Z=null;continue;}int tx=(int)Math.Floor(e.X.Value/64),tz=(int)Math.Floor(e.Z.Value/64);var key=(tx,tz);
   if(!masks.TryGetValue(key,out var mask)){mask=new byte[512];lock(gate)foreach(var owner in owners){string id=Key(world,owner,tx.ToString(),tz.ToString());var row=db.GetCollection("mapOverview").FindById(id);MapOverviewCell? cell=row==null?null:Read<MapOverviewCell>(row);if(cell==null){row=db.GetCollection("cells").FindById(id);if(row!=null)cell=MapOverviewCell.From(Read<MapCell>(row));}if(cell!=null){var bytes=Convert.FromBase64String(cell.ExplorationMask);for(int i=0;i<mask.Length;i++)mask[i]|=bytes[i];}}masks[key]=mask;}
   if(!MapOverviewCell.Known(mask,(int)Math.Floor(e.X.Value-tx*64),(int)Math.Floor(e.Z.Value-tz*64))){e.X=null;e.Z=null;}
  }
 }
 internal IReadOnlyList<SagaEvent> AnalyticsHistory(string world){lock(gate){if(analyticsHistory.TryGetValue(world,out var rows)){if(analyticsPending.TryGetValue(world,out var pending)&&pending.Count>0){rows=Array.AsReadOnly(rows.Concat(pending).ToArray());analyticsPending.Remove(world);if(analyticsHistory.Values.Sum(x=>x.Count)+pending.Count>100000)InvalidateAnalytics();else analyticsHistory[world]=rows;}return rows;}rows=Events(world,new TimeWindow(DateTime.MinValue,DateTime.MaxValue)).AsReadOnly();if(rows.Count<=100000){if(analyticsHistory.Count>=2||analyticsHistory.Values.Sum(x=>x.Count)+rows.Count>100000)InvalidateAnalytics();analyticsHistory[world]=rows;}return rows;}}
}
public sealed partial class SagaService {
 void SanitizePlayers(List<PlayerSnapshot> players,SlsWorldState sls){
  foreach(var p in players){p.Online=p.Online&&(DateTime.UtcNow-(p.LastSeenUtc??p.Utc)).TotalSeconds<=options.PresenceTimeoutSeconds;if(!p.SharePosition||!p.X.HasValue||!p.Z.HasValue){p.X=null;p.Z=null;p.PositionUtc=null;p.PositionLive=false;}else{p.PositionUtc=p.PositionUtc??p.Utc;p.PositionLive=p.Online&&(DateTime.UtcNow-p.PositionUtc.Value).TotalSeconds<=options.PresenceTimeoutSeconds;}if(!p.ShareProfile){p.CompletedBossKeys.Clear();p.ProgressionTier=0;p.ProgressionBoss="";p.LastAchievementUtc=null;p.ProfileBiome="";p.ProfileBiomeEvidence="";p.BackgroundUnlocked=false;p.BackgroundPreference="automatic";p.Gear.Clear();p.Hotbar.Clear();p.PortraitStatus="";p.EffectiveStats.Clear();p.EffectiveResistances.Clear();p.PortraitId="";p.Gold=null;p.EpicLootInstalled=false;p.JewelcraftingInstalled=false;}}

  foreach(var p in players)if(!p.ShareProfile||!sls.Installed||!sls.NemesisEnabled)p.NemesisScore=null;
 }
 static DateTime? AdventureSince(string? value){if(string.IsNullOrWhiteSpace(value))return null;if(!DateTime.TryParse(value,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal,out var date))throw new ArgumentException("Invalid recap date.");return date;}
 public object WorldState(string world){var sls=ActiveSls(world);var players=store.Players(world);SanitizePlayers(players,sls);return new{world,worlds=store.Worlds(),worldNames=store.WorldNames(),server=new{name=options.ServerName,address=options.ServerAddress},players,sls=SlsSummary(sls),synthetic=options.Synthetic,serverUtc=DateTime.UtcNow};}
 readonly object versionGate=new object();DateTime versionRead;object? versionInfo;
 public object VersionInfo(){lock(versionGate){if(versionInfo!=null&&(DateTime.UtcNow-versionRead).TotalSeconds<30)return versionInfo;string websiteVersion="",websiteHash="",expectedWebsiteHash="";
  try{var manifest=Path.Combine(options.WebDirectory,"version.json");if(File.Exists(manifest)&&new FileInfo(manifest).Length<=4096){var data=JObject.Parse(File.ReadAllText(manifest));var version=data["version"];var hash=data["appSha256"];if(version?.Type==JTokenType.String&&VersionPolicy.Valid((string?)version))websiteVersion=(string)version!;if(hash?.Type==JTokenType.String){var value=(string)hash!;if(value.Length==64&&value.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F')))expectedWebsiteHash=value;}}}catch(IOException){}catch(UnauthorizedAccessException){}catch(JsonException){}
  try{var app=Path.Combine(options.WebDirectory,"app.js");if(File.Exists(app))using(var sha=SHA256.Create())using(var stream=File.OpenRead(app))websiteHash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}catch(IOException){}catch(UnauthorizedAccessException){}
  bool mismatch=string.IsNullOrWhiteSpace(websiteVersion)||(!string.IsNullOrWhiteSpace(options.ServerVersion)&&websiteVersion!=options.ServerVersion)||expectedWebsiteHash==""||!string.Equals(websiteHash,expectedWebsiteHash,StringComparison.OrdinalIgnoreCase);
  versionRead=DateTime.UtcNow;return versionInfo=new{serverVersion=options.ServerVersion,websiteVersion,websiteHash,expectedWebsiteHash,mismatch};}}
 void WarnWebsiteMismatch(){
  if(string.IsNullOrWhiteSpace(options.ServerVersion))return;
  var info=JObject.FromObject(VersionInfo());if(info["mismatch"]?.Value<bool>()!=true)return;
  // Run once on the HTTP worker: stale JavaScript cannot display the newer browser warning.
  // Never include request credentials, tokens, host paths, or arbitrary manifest text.
  try{options.Log?.Invoke("WARNING: Sagas website files do not match server version "+options.ServerVersion+". Stop the server, replace the complete matching Sagas package including its web folder (preserve configuration and recorded data), restart, and reload website tabs. Missing, invalid, or mixed website files can leave an older map renderer running.");}catch{/* An external logger must not prevent the website from starting. */}
 }
}
