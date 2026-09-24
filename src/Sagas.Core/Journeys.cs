using System;
using System.Collections.Generic;
using System.Linq;
namespace ValheimSagas;

public sealed partial class SagaService {
 // Separate, bounded feed. No generated prose is interpreted as a location.
 IEnumerable<SagaChapter> VisibleJourneys(string world,Dictionary<string,PlayerSnapshot> players,HashSet<string>? selected){
  var consent=new HashSet<string>(players.Keys,StringComparer.Ordinal);
  return store.Chapters(world).Concat(store.ServerChapters(world)).Where(c=>GeneratedChapter(c)&&ChapterContextVisible(c)&&(c.Scope=="server"?SharedChapterVisible(c,consent)&&(selected==null||c.Participants.Any(p=>selected.Contains(p.PlayerId))):consent.Contains(c.PlayerId)&&(selected==null||selected.Contains(c.PlayerId))));
 }
 object JourneyChapter(SagaChapter c,bool full)=>new{id=c.Id,playerId=c.PlayerId,scope=c.Scope,title=c.Title,utc=c.Utc,fromUtc=c.FromUtc,toUtc=c.ToUtc,text=full?c.Text:"",excerpt=c.Text.Length>360?c.Text.Substring(0,360)+"…":c.Text,participants=c.Participants,model=full?c.Model:"",facts=full?c.Facts:new List<string>()};
 public object SagaJourneys(string world,HashSet<string>? selected=null,string before=""){
  var players=store.Players(world).Where(p=>p.ShareProfile).ToDictionary(p=>p.PlayerId,StringComparer.Ordinal);
  var ordered=VisibleJourneys(world,players,selected).OrderByDescending(c=>c.Utc).ThenBy(c=>c.Id,StringComparer.Ordinal).ToArray();
  int start=string.IsNullOrEmpty(before)?0:Array.FindIndex(ordered,c=>c.Id==before)+1;
  if(!string.IsNullOrEmpty(before)&&start==0)return new{world,chapters=new object[0],next=""};
  var page=ordered.Skip(start).Take(24).ToArray();
  var owners=new HashSet<string>(players.Values.Where(p=>p.ShareMap&&(selected==null||selected.Contains(p.PlayerId))).Select(p=>p.PlayerId),StringComparer.Ordinal);
  var history=store.AnalyticsHistory(world).ToDictionary(e=>e.Id,StringComparer.Ordinal);
  var rows=page.Select(c=>{AdventureMoment? anchor=null;foreach(var id in c.EventIds.Take(500)){if(!history.TryGetValue(id,out var e)||!owners.Contains(e.PlayerId))continue;var m=AdventureProjection(world,e,players,owners);if(m.X.HasValue&&m.Z.HasValue){anchor=m;break;}}return new{chapter=JourneyChapter(c,false),anchor};}).ToArray();
  return new{world,chapters=rows,next=ordered.Length>start+page.Length&&page.Length>0?page[page.Length-1].Id:""};
 }
 // Rank meaningful evidence, discourage repetitive loot/combat, then restore chronology.
 static AdventureMoment[] JourneyHighlights(List<AdventureMoment> source){
  if(source.Count<=6)return source.ToArray();
  var chosen=new List<int>();
  double Importance(AdventureMoment m)=>m.Boss||m.NemesisBoss?100:m.Kind=="death"?65:m.Kind=="bounty"?60:m.Kind=="collect"&&!string.IsNullOrWhiteSpace(m.Rarity)?55:m.Kind=="kill"?25+Math.Min(m.Stars,10)*3:5;
  while(chosen.Count<6){
   var best=Enumerable.Range(0,source.Count).Where(i=>!chosen.Contains(i)).OrderByDescending(i=>{
    var m=source[i];double score=Importance(m);
    if(chosen.Any(j=>source[j].Kind==m.Kind&&source[j].Prefab==m.Prefab&&source[j].Rarity==m.Rarity))score-=45;
    if(!chosen.Any(j=>source[j].Biome==m.Biome))score+=18;
    if(chosen.Count>0)score+=Math.Min(20,chosen.Min(j=>Math.Sqrt(Math.Pow(m.X!.Value-source[j].X!.Value,2)+Math.Pow(m.Z!.Value-source[j].Z!.Value,2)))/15);
    if(i==0||i==source.Count-1)score+=8;
    return score;
   }).ThenBy(i=>i).First();chosen.Add(best);
  }
  chosen.Sort();int previous=-1;var result=new List<AdventureMoment>();
  foreach(var i in chosen){var m=source[i];m.BreakBefore=previous<0||source.Skip(previous+1).Take(i-previous).Any(p=>p.BreakBefore)||m.PlayerId!=source[previous].PlayerId||m.Utc-source[previous].Utc>TimeSpan.FromMinutes(30)||Math.Pow(m.X!.Value-source[previous].X!.Value,2)+Math.Pow(m.Z!.Value-source[previous].Z!.Value,2)>1200*1200;result.Add(m);previous=i;}
  return result.ToArray();
 }
 static AdventureMoment[] SceneMoments(SagaChapter chapter,List<AdventureMoment> records){
  var result=new List<AdventureMoment>();var lookup=records.Select((m,i)=>new{m,i}).ToDictionary(x=>x.m.Id,StringComparer.Ordinal);int previous=-1;
  foreach(var scene in chapter.Scenes.Take(6)){
   // Hide the whole scene if any of its supporting locations are no longer shared/known.
   if(scene.EventIds.Count==0||scene.EventIds.Any(id=>!lookup.ContainsKey(id))){previous=-1;continue;}
   var evidence=scene.EventIds.Select(id=>lookup[id]).OrderBy(x=>x.i).ToArray();
   var anchor=evidence.FirstOrDefault(x=>x.m.Kind=="kill"&&(x.m.Boss||x.m.NemesisBoss))??evidence[0];var m=anchor.m;
   m.BreakBefore=previous<0||records.Skip(previous+1).Take(anchor.i-previous).Any(p=>p.BreakBefore)||m.PlayerId!=records[previous].PlayerId||m.Utc-records[previous].Utc>TimeSpan.FromMinutes(30)||Math.Pow(m.X!.Value-records[previous].X!.Value,2)+Math.Pow(m.Z!.Value-records[previous].Z!.Value,2)>1200*1200;
   m.SceneTitle=scene.Title;m.SceneText=scene.Text;m.EvidenceIds=scene.EventIds.ToList();result.Add(m);previous=anchor.i;
  }
  return result.ToArray();
 }
 public object SagaJourney(string world,string id,HashSet<string>? selected=null){
  var players=store.Players(world).Where(p=>p.ShareProfile).ToDictionary(p=>p.PlayerId,StringComparer.Ordinal);
  var chapter=VisibleJourneys(world,players,selected).FirstOrDefault(c=>c.Id==id);
  if(chapter==null)return new{world,chapter=(object?)null,moments=new AdventureMoment[0],truncated=false};
  var owners=new HashSet<string>(players.Values.Where(p=>p.ShareMap&&(selected==null||selected.Contains(p.PlayerId))).Select(p=>p.PlayerId),StringComparer.Ordinal);
  var ids=new HashSet<string>(chapter.EventIds,StringComparer.Ordinal);
  var records=store.AnalyticsHistory(world).Where(e=>ids.Contains(e.Id)).OrderBy(e=>e.Utc).ThenBy(e=>e.Id,StringComparer.Ordinal).Take(501).ToArray();
  var result=new List<AdventureMoment>();AdventureMoment? previous=null;int previousIndex=-2;
  for(int i=0;i<Math.Min(500,records.Length);i++){
   var e=records[i];if(!owners.Contains(e.PlayerId))continue;
   var m=AdventureProjection(world,e,players,owners);if(!m.X.HasValue||!m.Z.HasValue)continue;
   m.BreakBefore=previous==null||i!=previousIndex+1||previous.PlayerId!=m.PlayerId||m.Utc-previous.Utc>TimeSpan.FromMinutes(30)||Math.Pow(m.X.Value-previous.X!.Value,2)+Math.Pow(m.Z.Value-previous.Z!.Value,2)>1200*1200;
   result.Add(m);previous=m;previousIndex=i;
  }
  return new{world,chapter=JourneyChapter(chapter,true),moments=chapter.Scenes.Count>0?SceneMoments(chapter,result):JourneyHighlights(result),truncated=records.Length>500||(chapter.Scenes.Count==0&&result.Count>6),notes="Connections show the order of recorded moments, not travelled paths. Gaps, different Vikings, long intervals and distant locations break the trail."};
 }
}
