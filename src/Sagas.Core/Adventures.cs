using System;
using System.Collections.Generic;
using System.Linq;
namespace ValheimSagas;

public sealed partial class SagaService {
 // Event projections never mutate the shared analytics cache. Location lookups run only after bounded selection.
 static bool OrdinaryBoss(SagaEvent e)=>e.Kind=="kill"&&e.Boss&&!e.NemesisBoss&&!BossCatalog.IntermediatePhase(e.Prefab);
 static string[] AdventureTeam(SagaEvent e,Dictionary<string,PlayerSnapshot> players)=>e.Contributors.Concat(new[]{e.PlayerId}).Where(players.ContainsKey).Distinct(StringComparer.Ordinal).ToArray();
 AdventureMoment AdventureProjection(string world,SagaEvent e,Dictionary<string,PlayerSnapshot> players,HashSet<string> owners){
  bool known=players.ContainsKey(e.PlayerId)&&owners.Contains(e.PlayerId)&&e.X.HasValue&&e.Z.HasValue&&store.KnownPoint(world,owners,e.X.Value,e.Z.Value);
  return new AdventureMoment{Id=e.Id,Prefab=e.Prefab,Biome=e.Biome,Utc=e.Utc,Kind=e.Kind,Name=e.Name,PlayerId=players.ContainsKey(e.PlayerId)?e.PlayerId:"",PlayerName=players.TryGetValue(e.PlayerId,out var p)?p.Name:"",Stars=e.Stars,Amount=e.Amount,Rarity=e.Rarity,RarityColor=e.RarityColor,Boss=e.Boss,NemesisBoss=e.NemesisBoss,Team=AdventureTeam(e,players).Select(id=>new LeaderboardPerson{PlayerId=id,Name=players[id].Name}).ToArray(),X=known?e.X:null,Z=known?e.Z:null};
 }
 public object Adventures(string world,TimeWindow window,DateTime? since=null,HashSet<string>? selected=null){
  var players=store.Players(world).Where(p=>p.ShareProfile).ToDictionary(p=>p.PlayerId,StringComparer.Ordinal);
  var ids=new HashSet<string>(players.Keys.Where(id=>selected==null||selected.Contains(id)),StringComparer.Ordinal);
  var owners=new HashSet<string>(players.Values.Where(p=>p.ShareMap&&ids.Contains(p.PlayerId)).Select(p=>p.PlayerId),StringComparer.Ordinal);
  var history=store.AnalyticsHistory(world);
  bool InScope(SagaEvent e)=>ids.Contains(e.PlayerId)||((OrdinaryBoss(e)||e.NemesisBoss)&&AdventureTeam(e,players).Any(ids.Contains));
  var events=history.Where(e=>window.Contains(e.Utc)&&InScope(e)).ToArray();
  var bosses=events.Where(OrdinaryBoss).OrderBy(e=>e.Utc).ThenBy(e=>e.Id,StringComparer.Ordinal).ToArray();
  LeaderboardBossKill Kill(SagaEvent e){var identity=BossIdentity(e);return new LeaderboardBossKill{BossKey=identity.Key,BossName=identity.Name,Order=identity.Order,Utc=e.Utc,Stars=e.Stars,DurationSeconds=!BossCatalog.FinalNorthPhase(e.Prefab)&&e.DurationSeconds.HasValue&&e.DurationSeconds>0&&!double.IsNaN(e.DurationSeconds.Value)&&!double.IsInfinity(e.DurationSeconds.Value)?e.DurationSeconds:null,Team=AdventureTeam(e,players).Select(id=>new LeaderboardPerson{PlayerId=id,Name=players[id].Name}).ToArray(),Finisher=players.TryGetValue(e.PlayerId,out var p)?new LeaderboardPerson{PlayerId=p.PlayerId,Name=p.Name}:null};}
  var records=bosses.Select(Kill).ToArray();
  // Unlocks follow permanent receipts and retained all-time evidence, not the selected window.
  var earnedKeys=new HashSet<string>(ids.SelectMany(id=>store.CompletedBossKeys(world,id)),StringComparer.OrdinalIgnoreCase);
  var lifetimeBosses=history.Where(e=>OrdinaryBoss(e)&&InScope(e)).Select(BossIdentity).ToArray();
  foreach(var b in lifetimeBosses)earnedKeys.Add(b.Key);
  var trophyKeys=KnownBosses.Select(b=>(Key:b.Key,Name:b.Name,Order:(int?)b.Order)).Concat(lifetimeBosses.Where(b=>!b.Order.HasValue).Select(b=>(Key:b.Key,Name:b.Name,Order:b.Order))).GroupBy(b=>b.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First());
  var trophies=trophyKeys.Select(b=>{var rows=records.Where(r=>string.Equals(r.BossKey,b.Key,StringComparison.OrdinalIgnoreCase)).ToArray();return new{key=b.Key,name=b.Name,order=b.Order,earned=earnedKeys.Contains(b.Key),kills=rows.Length,uniquePlayers=rows.SelectMany(r=>r.Team).Select(p=>p.PlayerId).Distinct().Count(),first=rows.FirstOrDefault(),highestStars=rows.OrderByDescending(r=>r.Stars).ThenBy(r=>r.DurationSeconds.HasValue?0:1).ThenBy(r=>r.DurationSeconds).ThenBy(r=>r.Utc).FirstOrDefault(),fastest=rows.Where(r=>r.DurationSeconds.HasValue).OrderBy(r=>r.DurationSeconds).ThenBy(r=>r.Utc).FirstOrDefault()};}).OrderBy(b=>b.order??int.MaxValue).ThenBy(b=>b.key,StringComparer.Ordinal).ToArray();
  var comparisons=players.Values.Where(p=>ids.Contains(p.PlayerId)).OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>{var own=events.Where(e=>e.PlayerId==p.PlayerId).ToArray();var kills=own.Where(e=>e.Kind=="kill").ToArray();return new{playerId=p.PlayerId,name=p.Name,kills=kills.Length,deaths=own.Count(e=>e.Kind=="death"),bossParticipations=bosses.Count(e=>AdventureTeam(e,players).Contains(p.PlayerId)),nemesisBossParticipations=events.Count(e=>e.Kind=="kill"&&e.NemesisBoss&&AdventureTeam(e,players).Contains(p.PlayerId)),collected=own.Where(e=>e.Kind=="collect").Sum(e=>(long)e.Amount),drops=own.Where(e=>e.Kind=="drop").Sum(e=>(long)e.Amount),highestStars=kills.Length==0?(int?)null:kills.Max(e=>e.Stars),bounties=own.Count(e=>e.Kind=="bounty"),rarities=own.Where(e=>e.Kind=="collect").GroupBy(e=>LeaderboardRarity(e.Rarity)).ToDictionary(g=>g.Key,g=>g.Sum(e=>(long)e.Amount))};}).ToArray();
  var until=DateTime.UtcNow;var start=since??until.AddDays(-1);if(start>until)start=until;
  var recent=history.Where(e=>e.Utc>start&&e.Utc<=until&&InScope(e)).ToArray();
  var highlights=recent.Where(e=>OrdinaryBoss(e)||(e.Kind=="kill"&&(e.NemesisBoss||e.Stars>=2))||e.Kind=="death"||e.Kind=="bounty"||(e.Kind=="collect"&&!string.IsNullOrWhiteSpace(e.Rarity))).OrderByDescending(e=>e.Utc).ThenBy(e=>e.Id,StringComparer.Ordinal).ToArray();
  var chapters=store.Chapters(world).Concat(store.ServerChapters(world)).Where(c=>c.Utc>start&&c.Utc<=until&&GeneratedChapter(c)&&ChapterContextVisible(c)&&(c.Scope=="server"?SharedChapterVisible(c,new HashSet<string>(players.Keys))&&(selected==null||c.Participants.Any(p=>ids.Contains(p.PlayerId))):ids.Contains(c.PlayerId))).OrderByDescending(c=>c.Utc).Take(12).Select(c=>new{id=c.Id,playerId=c.PlayerId,scope=c.Scope,title=c.Title,utc=c.Utc}).ToArray();
  return new{world,from=window.From,to=window.To,since=start,partialHistory=window.From<store.TrackingSince||(store.StatisticsSince.HasValue&&window.From<store.StatisticsSince.Value),recap=new{from=start,to=until,partialHistory=start<store.TrackingSince||(store.StatisticsSince.HasValue&&start<store.StatisticsSince.Value),totals=new{kills=recent.Count(e=>e.Kind=="kill"&&ids.Contains(e.PlayerId)),deaths=recent.Count(e=>e.Kind=="death"),bossKills=recent.Count(OrdinaryBoss),collected=recent.Where(e=>e.Kind=="collect").Sum(e=>(long)e.Amount)},moments=highlights.Take(100).Select(e=>AdventureProjection(world,e,players,owners)).ToArray(),truncated=highlights.Length>100,chapters},trophies,comparisons,integrations=IntegrationSummary(players.Values.Where(p=>ids.Contains(p.PlayerId)),ActiveSls(world).Installed&&ActiveSls(world).NemesisEnabled),notes=new[]{"Trophies and comparisons follow the selected period; first means first retained victory within that period.","Boss participation includes recorded contributors and finisher. Private profiles are omitted, so a displayed team can be incomplete. Other kills use finishing-blow credit.","Collections exclude drops and unknown pickups. Imported exploration does not establish a dated discovery.","Fastest fights require a complete recorded duration; Deep North multi-phase fights have no complete duration yet.","Recap follows this browser's last visit, independently of the statistics period; its first visit covers the previous 24 hours."}};
 }
 public object ChapterMap(string world,string chapterId,HashSet<string>? selected=null){
  var players=store.Players(world).Where(p=>p.ShareProfile).ToDictionary(p=>p.PlayerId,StringComparer.Ordinal);var consent=new HashSet<string>(players.Keys,StringComparer.Ordinal);
  var chapter=store.Chapters(world).Concat(store.ServerChapters(world)).FirstOrDefault(c=>c.Id==chapterId&&GeneratedChapter(c)&&ChapterContextVisible(c)&&(c.Scope=="server"?SharedChapterVisible(c,consent):consent.Contains(c.PlayerId)));
  var owners=new HashSet<string>(players.Values.Where(p=>p.ShareMap&&(selected==null||selected.Contains(p.PlayerId))).Select(p=>p.PlayerId),StringComparer.Ordinal);
  if(chapter==null)return new{world,chapterId="",moments=new AdventureMoment[0],truncated=false};
  var eventIds=new HashSet<string>(chapter.EventIds,StringComparer.Ordinal);
  var candidates=store.AnalyticsHistory(world).Where(e=>eventIds.Contains(e.Id)&&players.ContainsKey(e.PlayerId)&&owners.Contains(e.PlayerId)&&e.X.HasValue&&e.Z.HasValue).OrderByDescending(e=>e.Utc).ThenBy(e=>e.Id,StringComparer.Ordinal).Take(500).ToArray();
  var moments=candidates.Select(e=>AdventureProjection(world,e,players,owners)).Where(e=>e.X.HasValue&&e.Z.HasValue).Take(101).ToArray();
  return new{world,chapterId=chapter?.Id??"",moments=moments.Take(100).ToArray(),truncated=moments.Length>100||candidates.Length==500};
 }
}
public sealed class AdventureMoment {
 public string SceneTitle {get;set;}="";public string SceneText {get;set;}="";public List<string> EvidenceIds {get;set;}=new List<string>();
 public string Prefab{get;set;}="";public string Biome{get;set;}="";public bool BreakBefore{get;set;} public string Id{get;set;}="";public DateTime Utc{get;set;}public string Kind{get;set;}="";public string Name{get;set;}="";public string PlayerId{get;set;}="";public string PlayerName{get;set;}="";public int Stars{get;set;}public int Amount{get;set;}public string Rarity{get;set;}="";public string RarityColor{get;set;}="";public bool Boss{get;set;}public bool NemesisBoss{get;set;}public LeaderboardPerson[] Team{get;set;}=new LeaderboardPerson[0];public float? X{get;set;}public float? Z{get;set;}
}
