using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ValheimSagas;

/// <summary>Server-built evidence only. Identifiers stay local and are never serialized into the prompt.</summary>
public sealed class LoreContext {
 public List<string> Facts {get;} = new List<string>();
 public List<SagaParticipant> Participants {get;} = new List<SagaParticipant>();
 public List<string> MapParticipants {get;} = new List<string>();
 public List<string> PrivateValues {get;} = new List<string>();
}
public sealed partial class SagaStore {
 public LoreContext NarrativeContext(string world,string? character=null) {
  lock(gate) {
   var result=new LoreContext();
   var all=Players(world);var shared=all.Where(p=>p.ShareProfile).ToDictionary(p=>p.PlayerId,StringComparer.Ordinal);
   result.PrivateValues.Add(world);result.PrivateValues.AddRange(all.Select(p=>p.PlayerId));
   if(character!=null&&!shared.ContainsKey(character))return result;
   var selected=shared.Values.Where(p=>character==null||p.PlayerId==character).OrderBy(p=>p.PlayerId,StringComparer.Ordinal).Take(8).ToArray();
   string Clean(string value,int limit=100){var s=new string((value??"").Where(c=>!char.IsControl(c)).ToArray());return s.Length>limit?s.Substring(0,limit):s;}
   string Stats(Dictionary<string,float> stats)=>string.Join(", ",stats.OrderBy(s=>s.Key,StringComparer.Ordinal).Take(12).Select(s=>Clean(s.Key,45)+"="+s.Value.ToString("0.##",CultureInfo.InvariantCulture)));
   foreach(var p in selected) {
    result.Participants.Add(new SagaParticipant{PlayerId=p.PlayerId,Name=p.Name});
    var prefix=Clean(p.Name)+" — last reported snapshot "+p.Utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'",CultureInfo.InvariantCulture)+": ";
    var gear=p.Gear.Where(g=>g.Equipped||g.Active).Take(10).ToArray();
    if(gear.Length>0)result.Facts.Add(prefix+"equipped/active gear: "+string.Join("; ",gear.Select(g=>Clean(g.Name,60)+" ["+Clean(g.Slot,25)+", quality "+g.Quality+(string.IsNullOrEmpty(g.Rarity)?"":", "+Clean(g.Rarity,25))+"]")));
    foreach(var g in gear.Where(g=>g.Effects.Count>0||g.Stats.Count>0).Take(3))result.Facts.Add(prefix+Clean(g.Name,60)+" item stats: "+Stats(g.Stats)+"; effects: "+string.Join("; ",g.Effects.Take(4).Select(e=>Clean(e,100))));
    foreach(var g in gear.Where(g=>g.Sockets.Count>0).Take(3))result.Facts.Add(prefix+Clean(g.Name,60)+" Jewelcrafting sockets (last equipment snapshot, not a new acquisition): "+string.Join("; ",g.Sockets.Select((socket,index)=>"slot "+(index+1)+": "+Clean(socket.Name==""?"empty or unavailable":socket.Name,60)))+". Gem power ranges are configuration, not observed combat totals.");
    if(p.EffectiveStats.Count>0||p.EffectiveResistances.Count>0)result.Facts.Add(prefix+"effective values (depend on skills, food, buffs and active weapon; not permanent traits): "+Stats(p.EffectiveStats)+"; resistances: "+string.Join(", ",p.EffectiveResistances.OrderBy(x=>x.Key,StringComparer.Ordinal).Take(10).Select(x=>Clean(x.Key,35)+"="+Clean(x.Value,35))));
    if(Sls(world).Installed&&Sls(world).NemesisEnabled&&p.NemesisScore.HasValue&&!float.IsNaN(p.NemesisScore.Value)&&!float.IsInfinity(p.NemesisScore.Value))result.Facts.Add(prefix+"current SLS Nemesis score "+p.NemesisScore.Value.ToString("0.##",CultureInfo.InvariantCulture)+" (last reported value, not a lifetime total, level, or boss victory).");
    if(p.Gold.HasValue)result.Facts.Add(prefix+"carried Coins balance "+p.Gold.Value+" (not lifetime earnings or total wealth).");
    if(p.ShareMap){var cells=Cells(world,new HashSet<string>{p.PlayerId});if(cells.Count>0){result.MapParticipants.Add(p.PlayerId);result.Facts.Add(Clean(p.Name)+" — shared explored map snapshot: "+cells.Count+" known 64m cells, an upper-bound cell footprint of "+(cells.Count*4096d/1000000).ToString("0.##",CultureInfo.InvariantCulture)+" square km (cells can be partially explored); biome labels: "+string.Join(", ",cells.Select(c=>Clean(c.Biome,40)).Distinct(StringComparer.Ordinal).OrderBy(b=>b,StringComparer.Ordinal).Take(12))+". Imported discoveries may predate tracking; no discovery dates or journeys are inferred.");}}
   }
   var history=Events(world,new TimeWindow(DateTime.MinValue,DateTime.UtcNow));
   result.PrivateValues.AddRange(history.Select(e=>e.Id));
   var owned=history.Where(e=>shared.ContainsKey(e.PlayerId)&&(character==null||e.PlayerId==character)).ToArray();
   foreach(var p in selected) {
    var events=owned.Where(e=>e.PlayerId==p.PlayerId).ToArray();
    var nemesis=history.Where(e=>e.Kind=="kill"&&e.NemesisBoss&&(e.PlayerId==p.PlayerId||e.Contributors.Contains(p.PlayerId))).Select(e=>e.Id).Distinct(StringComparer.Ordinal).Count();
    if(nemesis>0)result.Facts.Add(Clean(p.Name)+" - retained recorded SLS Nemesis boss defeats with contributor or finisher credit: "+nemesis+". These are Nemesis encounters, not evidence of vanilla boss progression.");
    var kills=events.Where(e=>e.Kind=="kill").ToArray();var loot=events.Where(e=>e.Kind=="collect").ToArray();
    result.Facts.Add(Clean(p.Name)+" - retained history: "+kills.Length+" finishing-blow kills; "+events.Count(e=>e.Kind=="death")+" character deaths; "+events.Count(e=>e.Kind=="join")+" recorded arrivals and "+events.Count(e=>e.Kind=="leave")+" departures (not necessarily complete sessions). Star distribution: "+string.Join(", ",kills.GroupBy(e=>e.Stars).OrderByDescending(g=>g.Key).Take(8).Select(g=>g.Key+" stars: "+g.Count()))+"; collected rarity quantities: "+string.Join(", ",loot.GroupBy(e=>string.IsNullOrWhiteSpace(e.Rarity)?"No Epic Loot rarity":e.Rarity).OrderByDescending(g=>g.Sum(e=>(long)e.Amount)).Take(8).Select(g=>Clean(g.Key,30)+"="+g.Sum(e=>(long)e.Amount)))+"; observed drops "+events.Where(e=>e.Kind=="drop").Sum(e=>(long)e.Amount)+" (not acquired items).");
    if(p.ShareMap){var biomes=events.Select(e=>e.Biome).Where(b=>!string.IsNullOrWhiteSpace(b)).Distinct(StringComparer.Ordinal).Take(12).ToArray();if(biomes.Length>0){result.MapParticipants.Add(p.PlayerId);result.Facts.Add(Clean(p.Name)+" - biome labels in shared recorded adventures: "+string.Join(", ",biomes.Select(b=>Clean(b,40)))+". These do not assert completion or chronology of exploration.");}}
   }
   foreach(var group in owned.Where(e=>e.Kind=="bounty").GroupBy(e=>e.PlayerId).OrderBy(g=>g.Key,StringComparer.Ordinal).Take(8))result.Facts.Add(Clean(shared[group.Key].Name)+" — retained recorded bounty completions: "+group.Count()+"; recent targets: "+string.Join(", ",group.OrderByDescending(e=>e.Utc).Take(3).Select(e=>Clean(e.Name)))+". Earlier unrecorded bounties are unknown.");
   foreach(var e in history.Where(e=>e.Kind=="kill"&&(e.Boss||e.NemesisBoss)&&(character==null?shared.ContainsKey(e.PlayerId)||e.Contributors.Any(shared.ContainsKey):e.PlayerId==character||e.Contributors.Contains(character))).OrderByDescending(e=>e.Utc).Take(8)) {
    var team=e.Contributors.Concat(new[]{e.PlayerId}).Where(shared.ContainsKey).Distinct(StringComparer.Ordinal).Select(id=>shared[id]).ToArray();
    foreach(var p in team)result.Participants.Add(new SagaParticipant{PlayerId=p.PlayerId,Name=p.Name});
    result.Facts.Add((e.NemesisBoss?"SLS Nemesis boss record ":"Boss record ")+e.Utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'",CultureInfo.InvariantCulture)+": "+Clean(e.Name)+", "+e.Stars+" stars; named recorded contributors: "+string.Join(", ",team.Take(8).Select(p=>Clean(p.Name)))+". Contributor list may omit private profiles; this is not proof of a solo victory."+(e.DurationSeconds.HasValue?" Observed fight duration "+e.DurationSeconds.Value.ToString("0.#",CultureInfo.InvariantCulture)+" seconds.":" Fight duration unavailable."));
   }
   return result;
  }
 }
}
