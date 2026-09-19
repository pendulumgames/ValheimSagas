using ValheimSagas;
internal static class RetentionChecks {
 public static void Run(string root,Action<bool,string> check) {
  var dir=Path.Combine(root,"retention");var now=DateTime.UtcNow;
  using(var store=new SagaStore(dir)){
   var e=new SagaEvent{Id="old",World="retention",PlayerId="a",Kind="kill",Prefab="Troll",Name="Troll",Stars=4,Amount=1,Utc=now.AddDays(-10),X=12,Z=12,Effects=new(){"detail"}};
   store.AddEvent(e);store.AddEvent(new(){Id="recent",World=e.World,PlayerId="a",Kind="kill",Prefab="Boar",Name="Boar",Utc=now.AddDays(-1)});
   check(store.ApplyRetention(now,7,0)==1,"Compact old detail");var all=store.Events(e.World,new(DateTime.MinValue,now));check(all.Count==2,"Retained exact statistical totals");var archived=all.Single(x=>x.Id=="old");check(archived.DetailsExpired&&archived.X==null&&archived.Effects.Count==0&&archived.Stars==4,"Remove detail preserve breakdown");
   check(store.Events(e.World,new(e.Utc,e.Utc)).Count==1,"Archived custom boundary exact");check(!store.AddEvent(e),"Archived event dedup survives");check(store.Events(e.World,new(e.Utc.AddTicks(1),e.Utc.AddMilliseconds(1))).Count==0,"Sub-millisecond custom boundary remains exact through indexed BSON dates");
   var drop=new SagaEvent{Id="drop",World=e.World,Kind="drop",Amount=5,Provenance="p",Prefab="Resin",Name="Resin",Quality=2,Rarity="Magic",Utc=now.AddDays(-12)};store.AddEvent(drop);
   store.AddEvent(new(){Id="collection",World=e.World,PlayerId="a",Kind="collect",Amount=2,Provenance="p",Prefab="FakeGold",Name="Fake",Quality=99,Utc=now});
   var earned=store.Events(e.World,new(DateTime.MinValue,now)).Single(x=>x.Id=="collection");check(earned.Prefab=="Resin"&&earned.Quality==2&&earned.Rarity=="Magic","Collection metadata bound to emitted drop");
   store.ApplyRetention(now,7,8);check(!store.Events(e.World,new(DateTime.MinValue,now)).Any(x=>x.Id=="old"),"Statistics retention expires history");check(store.StatisticsSince.HasValue,"Partial history cutoff retained");check(!store.AddEvent(e),"Purged events still dedup");
   store.AddEvent(new(){Id="collection2",World=e.World,PlayerId="a",Kind="collect",Amount=20,Provenance="p",Utc=now});check(store.Events(e.World,new(DateTime.MinValue,now)).Where(x=>x.Kind=="collect").Sum(x=>x.Amount)==5,"Expired drop keeps provenance ceiling");
  }
  using(var store=new SagaStore(dir))check(store.StatisticsSince.HasValue,"Retention watermark persists restart");
 }
}
