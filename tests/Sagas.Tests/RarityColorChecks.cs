using ValheimSagas;
using Newtonsoft.Json.Linq;
static class RarityColorChecks {
 public static void Run(string root,Action<bool,string> check){
  check(RarityColors.Normalize("#abc")=="#AABBCC"&&RarityColors.Normalize("#1234")=="#112233","Epic Loot short hex colors normalize without transparency");
  check(RarityColors.Normalize("#12abEF80")=="#12ABEF","Epic Loot alpha hex is normalized to readable opaque RGB");
  check(RarityColors.Normalize("red; background:url(https://invalid)")==""&&!RarityColors.Valid("#xyzxyz"),"Only safe hex colors accepted");
  check(RarityColors.Valid("")&&!RarityColors.Valid(null),"Older snapshots retain empty palette fallback");
  var dir=Path.Combine(root,"rarity");var utc=DateTime.UtcNow;
  using(var service=new SagaService(new(){DataDirectory=dir,World="colors"})){
   var drop=new SagaEvent{Id="drop",World="colors",Kind="drop",Provenance="origin",Rarity="Legendary",RarityColor="#12ABEF",Utc=utc};
   var collect=new SagaEvent{Id="collect",World="colors",Kind="collect",Provenance="origin",RarityColor="#FFFFFF",Utc=utc};
   service.Store.AddEvent(collect);service.Store.AddEvent(drop);
   check(service.Store.Events("colors",new(DateTime.MinValue,DateTime.MaxValue)).Single(e=>e.Kind=="collect").RarityColor=="#12ABEF","Deferred collection inherits authoritative drop color");
   var state=JObject.FromObject(service.State("colors",new(DateTime.MinValue,DateTime.MaxValue)));
   check(state["stats"]!["loot"]!.All(x=>(string?)x["rarityColor"]=="#12ABEF"),"Loot aggregate exposes resolved rarity color");
   drop.RarityColor="url(https://invalid)";check(!SagaService.ValidEvent(drop),"Reject injected event colors");
  }
  using(var store=new SagaStore(dir))check(store.Events("colors",new(DateTime.MinValue,DateTime.MaxValue)).All(e=>e.RarityColor=="#12ABEF"),"Rarity color survives persistence");
 }
}
