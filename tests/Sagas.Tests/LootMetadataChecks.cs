using ValheimSagas;
internal static class LootMetadataChecks {
 public static void Run(string root,Action<bool,string> check) {
  var data=new Dictionary<string,string>{{"sagas.origin","drop-a"},{"sagas.source","creature"},{"magic","keep"},{"sockets","keep-too"},{"sagas.other","not-ours"}};
  check(LootTrackingMetadata.RemoveLegacy(data),"Remove obsolete Sagas inventory tags");
  check(data.Count==3&&data["magic"]=="keep"&&data["sockets"]=="keep-too"&&data["sagas.other"]=="not-ours","Preserve all other metadata exactly");
  check(!LootTrackingMetadata.RemoveLegacy(data),"Migration idempotent");
  check(!LootTrackingMetadata.RemoveLegacy(null),"Missing custom data safe");
  var mixed=new Dictionary<string,string>{{"sagas.source","mixed-ground-stack"}};
  check(LootTrackingMetadata.RemoveLegacy(mixed)&&mixed.Count==0,"Mixed source tag removed too");
  using var store=new SagaStore(Path.Combine(root,"loot-metadata"));var now=DateTime.UtcNow;
  SagaEvent E(string id,string kind,string origin,int amount)=>new(){Id=id,Kind=kind,World="migration",PlayerId="viking",Utc=now,Provenance=origin,Amount=amount,Prefab="Resin",Name="Resin"};
  check(store.AddEvent(E("collect-a","collect","ground-a",3)),"Collection can arrive before drop");
  check(store.AddEvent(E("drop-a","drop","ground-a",3)),"Ground object provenance resolves pending collection");
  check(!store.AddEvent(E("collect-a","collect","ground-a",3)),"Duplicate pickup report rejected");
  check(!store.AddEvent(E("collect-retry","collect","ground-a",3)),"Different report cannot exceed original drop");
  check(store.AddEvent(E("redrop","pickup","",3)),"Player redrop remains ordinary pickup");
  check(store.AddEvent(E("mixed","pickup","",6)),"Unknown ground merge remains ordinary pickup");
  check(store.Events("migration",new(DateTime.MinValue,now)).Where(e=>e.Kind=="collect").Sum(e=>e.Amount)==3,"Redrops and merges do not mint earned loot");
 }
}
