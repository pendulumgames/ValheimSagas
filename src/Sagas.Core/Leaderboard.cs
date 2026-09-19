using System;
using System.Collections.Generic;
using System.Linq;

namespace ValheimSagas;

public sealed partial class SagaService {
 static readonly (string Key,string Name,int Order,string Biome)[] KnownBosses={
  ("Eikthyr","Eikthyr",1,"Meadows"),("gd_king","The Elder",2,"BlackForest"),("Bonemass","Bonemass",3,"Swamp"),("Dragon","Moder",4,"Mountain"),
  ("GoblinKing","Yagluth",5,"Plains"),("SeekerQueen","The Queen",6,"Mistlands"),("Fader","Fader",7,"Ashlands")
 };
 static string LeaderboardRarity(string rarity)=>string.IsNullOrWhiteSpace(rarity)?"Unenchanted":rarity.Trim();
 static string BossPrefab(SagaEvent e)=>e.Prefab.EndsWith("(Clone)",StringComparison.Ordinal)?e.Prefab.Substring(0,e.Prefab.Length-7).Trim():e.Prefab;
 static (string Key,string Name,int? Order) BossIdentity(SagaEvent e){
  var prefab=BossPrefab(e);foreach(var boss in KnownBosses)if(string.Equals(prefab,boss.Key,StringComparison.OrdinalIgnoreCase))return(boss.Key,boss.Name,boss.Order);
  return(string.IsNullOrWhiteSpace(prefab)?"unknown:"+e.Name:prefab,string.IsNullOrWhiteSpace(e.Name)?prefab:e.Name,null);
 }
 public object Leaderboard(string world,TimeWindow window,HashSet<string>? selected=null){
  var players=store.Players(world).Where(p=>p.ShareProfile&&(selected==null||selected.Contains(p.PlayerId))).ToDictionary(p=>p.PlayerId,StringComparer.Ordinal);
  // Query the complete retained statistical ledger, never State's 5,000-row display subset.
  // Contributor credit can belong to a selected participant even when another player finished.
  var events=store.Events(world,window);
  LeaderboardPerson Person(string id)=>new LeaderboardPerson{PlayerId=id,Name=players[id].Name};
  var bossEvents=events.Where(e=>e.Kind=="kill"&&e.Boss).Select(e=>{
   var identity=BossIdentity(e);var team=e.Contributors.Concat(new[]{e.PlayerId}).Where(id=>!string.IsNullOrEmpty(id)&&players.ContainsKey(id)).Distinct(StringComparer.Ordinal).Select(Person).OrderBy(p=>p.Name,StringComparer.Ordinal).ThenBy(p=>p.PlayerId,StringComparer.Ordinal).ToArray();
   return new {Event=e,Row=new LeaderboardBossKill{BossKey=identity.Key,BossName=identity.Name,Order=identity.Order,Utc=e.Utc,DurationSeconds=e.DurationSeconds.HasValue&&e.DurationSeconds.Value>0&&!double.IsNaN(e.DurationSeconds.Value)&&!double.IsInfinity(e.DurationSeconds.Value)?e.DurationSeconds:null,Stars=e.Stars,Team=team,Finisher=players.ContainsKey(e.PlayerId)?Person(e.PlayerId):null}};
  }).Where(x=>x.Row.Team.Length>0).OrderBy(x=>x.Event.Utc).ThenBy(x=>x.Event.Id,StringComparer.Ordinal).ToArray();
  var identities=KnownBosses.Select(b=>(b.Key,b.Name,Order:(int?)b.Order)).Concat(bossEvents.Where(x=>!x.Row.Order.HasValue).Select(x=>(Key:x.Row.BossKey,Name:x.Row.BossName,Order:(int?)null)).GroupBy(x=>x.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First())).ToArray();
  var fastest=bossEvents.Where(x=>x.Row.DurationSeconds.HasValue).OrderBy(x=>x.Row.DurationSeconds).ThenBy(x=>x.Row.Utc).ThenBy(x=>x.Row.BossKey,StringComparer.Ordinal).ThenBy(x=>x.Event.Id,StringComparer.Ordinal).Select(x=>x.Row).ToArray();
  var bosses=identities.Select(b=>{
   var records=bossEvents.Where(x=>string.Equals(x.Row.BossKey,b.Key,StringComparison.OrdinalIgnoreCase)).ToArray();
   var credited=records.SelectMany(x=>x.Row.Team).GroupBy(p=>p.PlayerId,StringComparer.Ordinal).Select(g=>g.First()).OrderBy(p=>p.Name,StringComparer.Ordinal).ThenBy(p=>p.PlayerId,StringComparer.Ordinal).ToArray();
   return new{key=b.Key,name=b.Name,order=b.Order,kills=records.Length,uniquePlayers=credited.Length,players=credited,fastest=fastest.FirstOrDefault(x=>string.Equals(x.BossKey,b.Key,StringComparison.OrdinalIgnoreCase))};
  }).OrderBy(b=>b.order??int.MaxValue).ThenBy(b=>b.key,StringComparer.Ordinal).ToArray();
  var progression=players.Values.Select(p=>{
   var defeated=bossEvents.Where(x=>x.Row.Team.Any(person=>person.PlayerId==p.PlayerId)).GroupBy(x=>x.Row.BossKey,StringComparer.OrdinalIgnoreCase).Select(g=>{
    var first=g.First().Row;return new LeaderboardBossProgress{Key=first.BossKey,Name=first.BossName,Order=first.Order,FirstUtc=first.Utc,LastUtc=g.Last().Row.Utc,Kills=g.Count(),BestDurationSeconds=g.Where(x=>x.Row.DurationSeconds.HasValue).Select(x=>x.Row.DurationSeconds).DefaultIfEmpty(null).Min(),FirstTeam=first.Team};
   }).OrderBy(b=>b.Order??int.MaxValue).ThenBy(b=>b.Key,StringComparer.Ordinal).ToArray();
   var furthest=defeated.Where(b=>b.Order.HasValue).OrderByDescending(b=>b.Order).FirstOrDefault();
   return new LeaderboardProgress{PlayerId=p.PlayerId,Name=p.Name,HighestOrder=furthest?.Order??0,HighestBoss=furthest?.Name,BossCount=defeated.Count(b=>b.Order.HasValue),Bosses=defeated};
  }).Where(p=>p.Bosses.Length>0).OrderByDescending(p=>p.HighestOrder).ThenByDescending(p=>p.BossCount).ThenBy(p=>p.Name,StringComparer.Ordinal).ThenBy(p=>p.PlayerId,StringComparer.Ordinal).ToArray();
  for(int i=0;i<progression.Length;i++)progression[i].Rank=i>0&&progression[i].HighestOrder==progression[i-1].HighestOrder&&progression[i].BossCount==progression[i-1].BossCount?progression[i-1].Rank:i+1;
  LeaderboardRank[] Rank(IEnumerable<KeyValuePair<string,long>> values){
   var rows=values.Where(pair=>players.ContainsKey(pair.Key)).Select(pair=>new LeaderboardRank{PlayerId=pair.Key,Name=players[pair.Key].Name,Value=pair.Value}).OrderByDescending(row=>row.Value).ThenBy(row=>row.Name,StringComparer.Ordinal).ThenBy(row=>row.PlayerId,StringComparer.Ordinal).ToArray();
   for(int i=0;i<rows.Length;i++)rows[i].Rank=i>0&&rows[i].Value==rows[i-1].Value?rows[i-1].Rank:i+1;return rows;
  }
  var kills=events.Where(e=>e.Kind=="kill"&&players.ContainsKey(e.PlayerId)).ToArray();
  var killRows=Rank(kills.GroupBy(e=>e.PlayerId).Select(g=>new KeyValuePair<string,long>(g.Key,g.LongCount())));
  var starEvidence=kills.GroupBy(e=>e.PlayerId).ToDictionary(g=>g.Key,g=>g.OrderByDescending(e=>e.Stars).ThenBy(e=>e.Utc).ThenBy(e=>e.Id,StringComparer.Ordinal).First(),StringComparer.Ordinal);
  var starRows=Rank(starEvidence.Select(pair=>new KeyValuePair<string,long>(pair.Key,pair.Value.Stars))).Select(row=>new LeaderboardStarRank{Rank=row.Rank,PlayerId=row.PlayerId,Name=row.Name,Value=row.Value,CreatureName=starEvidence[row.PlayerId].Name,Prefab=starEvidence[row.PlayerId].Prefab,Utc=starEvidence[row.PlayerId].Utc}).ToArray();
  var collections=events.Where(e=>e.Kind=="collect"&&players.ContainsKey(e.PlayerId)).GroupBy(e=>LeaderboardRarity(e.Rarity),StringComparer.OrdinalIgnoreCase).Select(g=>new{rarity=g.Key,rarityColor=g.OrderByDescending(e=>e.Utc).ThenBy(e=>e.Id,StringComparer.Ordinal).Select(e=>e.RarityColor).FirstOrDefault(c=>!string.IsNullOrEmpty(c))??"",rows=Rank(g.GroupBy(e=>e.PlayerId).Select(p=>new KeyValuePair<string,long>(p.Key,p.Sum(e=>(long)e.Amount))))}).OrderBy(g=>g.rarity,StringComparer.OrdinalIgnoreCase).ToArray();
  LeaderboardSnapshotRank[] Snapshots(Func<PlayerSnapshot,long?> metric){
   var ranked=Rank(players.Values.Where(p=>metric(p).HasValue).Select(p=>new KeyValuePair<string,long>(p.PlayerId,metric(p)!.Value)));
   return ranked.Select(p=>new LeaderboardSnapshotRank{Rank=p.Rank,PlayerId=p.PlayerId,Name=p.Name,Value=p.Value,Utc=players[p.PlayerId].Utc}).ToArray();
  }
  var gold=Snapshots(p=>p.Gold);var bountyEvents=events.Where(e=>e.Kind=="bounty"&&players.ContainsKey(e.PlayerId)).ToArray();var bounties=Rank(bountyEvents.GroupBy(e=>e.PlayerId).Select(g=>new KeyValuePair<string,long>(g.Key,g.LongCount())));
  return new{server=new{name=options.ServerName,address=options.ServerAddress},world,from=window.From,to=window.To,trackingSince=store.TrackingSince,statisticsSince=store.StatisticsSince,partialHistory=window.From<store.TrackingSince||(store.StatisticsSince.HasValue&&window.From<store.StatisticsSince.Value),scope="selected-window",snapshotScope="latest-character-snapshot",synthetic=options.Synthetic,
   bosses,progression,fastestBossKills=fastest,kills=killRows,highestStars=starRows,collectionsByRarity=collections,gold,bounties,
   availability=new{timedBossKills=fastest.Length,untimedBossKills=bossEvents.Length-fastest.Length,goldSnapshots=gold.Length,bountyEvents=bountyEvents.Length,bountyTrackingAvailable=bountyEvents.Length>0||players.Values.Any(p=>p.EpicLootInstalled),epicLootPlayers=players.Values.Count(p=>p.EpicLootInstalled)},
   notes=new[]{"Boss credit is the distinct recorded contributors plus the finisher. Only currently shared profiles are listed; a displayed team may therefore be incomplete.","Progression ranks the highest known vanilla boss tier, then the number of distinct known bosses. It does not imply every earlier tier was completed; modded bosses have no invented progression tier.","Kill and highest-star rankings use finishing-blow credit. Collection rankings exclude drops and unknown pickups. All event panels follow the selected window.","Duration runs from the first observed damaging hit on a full-health boss to death. Partial fights and untimed legacy reports are omitted from speed rankings.","Gold is the last reported carried Coins balance and does not follow the selected time window. Bounties count recorded Epic Loot completion transitions within the selected window; older pruned history is not imported.","Equal scores share competition rank; display order breaks ties by name and character ID. Only retained history is available."}};
 }
}
public sealed class LeaderboardPerson {public string PlayerId{get;set;}="";public string Name{get;set;}="";}
public sealed class LeaderboardBossKill {public string BossKey{get;set;}="";public string BossName{get;set;}="";public int? Order{get;set;}public DateTime Utc{get;set;}public double? DurationSeconds{get;set;}public int Stars{get;set;}public LeaderboardPerson[] Team{get;set;}=new LeaderboardPerson[0];public LeaderboardPerson? Finisher{get;set;}}
public sealed class LeaderboardBossProgress {public string Key{get;set;}="";public string Name{get;set;}="";public int? Order{get;set;}public DateTime FirstUtc{get;set;}public DateTime LastUtc{get;set;}public int Kills{get;set;}public double? BestDurationSeconds{get;set;}public LeaderboardPerson[] FirstTeam{get;set;}=new LeaderboardPerson[0];}
public sealed class LeaderboardProgress {public int Rank{get;set;}public string PlayerId{get;set;}="";public string Name{get;set;}="";public int HighestOrder{get;set;}public string? HighestBoss{get;set;}public int BossCount{get;set;}public LeaderboardBossProgress[] Bosses{get;set;}=new LeaderboardBossProgress[0];}
public class LeaderboardRank {public int Rank{get;set;}public string PlayerId{get;set;}="";public string Name{get;set;}="";public long Value{get;set;}}
public sealed class LeaderboardSnapshotRank:LeaderboardRank {public DateTime Utc{get;set;}}
public sealed class LeaderboardStarRank:LeaderboardRank {public string CreatureName{get;set;}="";public string Prefab{get;set;}="";public DateTime Utc{get;set;}}
