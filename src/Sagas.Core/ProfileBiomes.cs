using System;
using System.Collections.Generic;
using System.Linq;

namespace ValheimSagas;

public sealed partial class SagaService {
 // Presentation is derived only from recorded boss participation. Client snapshot claims,
 // inventory, exploration and the currently selected analytics window provide no evidence.
 void ApplyProfileBiomes(IReadOnlyList<PlayerSnapshot> players,IReadOnlyList<SagaEvent> history){
  var latest=new Dictionary<string,DateTime>(StringComparer.Ordinal);
  foreach(var e in history.Where(e=>e.Kind=="kill"&&e.Boss&&!e.NemesisBoss&&KnownBosses.Any(b=>b.Key.Equals(BossCatalog.Prefab(e.Prefab),StringComparison.OrdinalIgnoreCase))))
   foreach(var id in e.Contributors.Concat(new[]{e.PlayerId}).Where(id=>!string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
    if(!latest.TryGetValue(id,out var prior)||e.Utc>prior)latest[id]=e.Utc;

  foreach(var player in players){
   player.ProfileBiome=player.ShareProfile?"Meadows":"";
   player.ProfileBiomeEvidence=player.ShareProfile?"Ambient Meadows backdrop; no recorded boss victory":"";
  }
  foreach(var player in players){
   // Canonical receipts preserve earned progression even after statistical retention.
   var receipts=store.CompletedBossKeys(player.World,player.PlayerId);
   var boss=KnownBosses.Where(b=>receipts.Contains(b.Key)).OrderByDescending(b=>b.Order).FirstOrDefault();
   player.ProgressionTier=player.ShareProfile?boss.Order:0;
   player.ProgressionBoss=player.ShareProfile?(boss.Name??""):"";
   player.LastAchievementUtc=player.ShareProfile&&latest.TryGetValue(player.PlayerId,out var last)?(DateTime?)last:null;
   if(player.ShareProfile&&boss.Order>0){
    player.ProfileBiome=new[]{"Meadows","BlackForest","Swamp","Mountain","Plains","Mistlands","Ashlands","DeepNorth"}[Math.Min(7,boss.Order)];
    // Deliberately omit teammate names/IDs: teammates may have private profiles.
    player.ProfileBiomeEvidence="Recorded team victory over "+boss.Name;
   }
   player.BackgroundUnlocked=player.ShareProfile;
   player.BackgroundPreference=player.BackgroundUnlocked?store.BackgroundPreference(player.World,player.PlayerId):"automatic";
   if(player.BackgroundUnlocked&&player.BackgroundPreference!="automatic"&&AllowedBackgrounds(player.World,player.PlayerId).Contains(player.BackgroundPreference)){
    player.ProfileBiome=BackgroundBiomes[player.BackgroundPreference];
    player.ProfileBiomeEvidence="Chosen by this Viking within their recorded boss progression";
   }
  }
 }
}
