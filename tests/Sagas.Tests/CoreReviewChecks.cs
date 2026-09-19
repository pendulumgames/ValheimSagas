using Newtonsoft.Json.Linq;
using ValheimSagas;

static class CoreReviewChecks {
 public static async Task Run(string root,Action<bool,string> check) {
  var all=new TimeWindow(DateTime.MinValue,DateTime.MaxValue);
  var past=DateTime.UtcNow.AddDays(-1);
  SagaEvent Event(string id,string kind="kill",int amount=1,string provenance="")=>new(){Id=id,World="review",PlayerId="p",PlayerName="Astrid",Kind=kind,Amount=amount,Provenance=provenance,Utc=past,Name="Troll",Prefab="Troll",X=32,Z=32};
  string reordered=Path.Combine(root,"reordered");
  using(var store=new SagaStore(reordered)) {
   check(!store.AddEvent(Event("empty","collect",5)),"Unprovenanced collection cannot mint earned loot");
   check(store.AddEvent(Event("before","collect",3,"item")),"Collection before drop is durably accepted");
   check(store.Events("review",all).Count==0,"Pending collection excluded from statistics");
   check(!store.AddEvent(Event("before","collect",3,"item")),"Pending receipt deduplicated");
   store.Explore(new(){World="review",PlayerId="p",Imported=true,Cells=new(){new(){X=0,Z=0}}});
   store.Explore(new(){World="review",PlayerId="p",Imported=false,Cells=new(){new(){X=0,Z=0}}});
   check(store.Imported("review",new(){"p"}),"Imported provenance survives refreshed cell");
  }
  using(var store=new SagaStore(reordered)) {
   check(store.AddEvent(Event("before2","collect",8,"item")),"Second pending partial collection");
   var foreign=Event("foreign","drop",99,"item");foreign.World="other";store.AddEvent(foreign);
   check(store.Events("review",all).Count==0,"Pending provenance isolated by world");
   check(store.AddEvent(Event("drop","drop",5,"item")),"Matching drop atomically resolves pending collections");
   var earned=store.Events("review",all).Where(e=>e.Kind=="collect").ToArray();
   check(earned.Length==2&&earned.Sum(e=>e.Amount)==5,"Pending replay capped across partial collections after restart");
   check(!store.AddEvent(Event("before","collect",3,"item")),"Resolved pending receipt remains deduplicated");
   check(!store.AddEvent(Event("drop-alias","drop",5,"item")),"One drop per provenance even under a new report id");
   check(!store.AddEvent(Event("again","collect",8,"item")),"Exhausted collection cannot create earned loot");
  }
  string servicePath=Path.Combine(root,"review-service");
  using(var service=new SagaService(new(){DataDirectory=servicePath,ListenPrefix="",LoreMilestoneEvents=99999,PresenceTimeoutSeconds=10})) {
   service.Start();
   bool? receipt=null;
   check(service.TryEvent(Event("accepted"),ok=>receipt=ok),"Durable callback event queued");
   check(service.Flush()&&receipt==true&&service.Store.Events("review",all).Any(e=>e.Id=="accepted"),"Positive ACK follows committed event");
   bool? mapReceipt=null;
   check(service.Explore(new(){World="review",PlayerId="p",Cells=new(){new(){X=0,Z=0}}},ok=>mapReceipt=ok),"Exploration durable callback queued");
   check(service.Flush()&&mapReceipt==true&&service.Store.Cells("review",new(){"p"}).Count==1,"Exploration ACK follows committed cells");
   var snapshot=new PlayerSnapshot{World="review",PlayerId="p",Name="Astrid",Online=false,ShareMap=true,SharePosition=true,ShareProfile=false,Utc=past,Gear=new(){new(){Name="Old sword",Slot="Weapon"}}};
   service.Store.Player(snapshot);
   check(service.TouchPresence("review","p","Astrid II")&&service.Flush(),"Server connection heartbeat queued");
   var touched=service.Store.Players("review").Single();
   check(touched.Utc==past&&touched.Gear.Count==1&&touched.ShareMap&&!touched.ShareProfile,"Heartbeat preserves gear timestamp and privacy settings");
   check(touched.Online&&touched.LastSeenUtc>past&&touched.Name=="Astrid II","Heartbeat refreshes presence and display name");
   service.TouchPresence("review","p","Astrid II",false);service.Flush();
   check(!service.Store.Players("review").Single().SharePosition,"Authoritative public-position revocation applied without client snapshot");
   check(!service.UpdatePlayer(new(){World="review",PlayerId="bad",EffectiveStats=null!}),"Malformed null effective values rejected");
   check(!service.UpdatePlayer(new(){World="review",PlayerId="bad",Gear=new(){null!}}),"Malformed null gear entry rejected");
   service.TouchPresence("review","p","Astrid II");service.Flush();
   check(service.Store.Events("review",all).Count(e=>e.Kind=="join")==1,"Repeated heartbeat does not duplicate join");
   var state=JObject.FromObject(service.State("review",all));
   check((bool)state["players"]![0]!["Online"]!&&state["players"]![0]!["X"]!.Type==JTokenType.Null,"Fresh presence does not revive stale coordinates");
   check(state["events"]!.Count()==0&&state["players"]![0]!["Gear"]!.Count()==0,"Private profile excludes recorded events and equipment");
   service.SetOffline("review","p");service.SetOffline("review","p");service.Flush();
   check(service.Store.Events("review",all).Count(e=>e.Kind=="leave")==1&&service.Store.Players("review").Single().Utc==past,"One leave per transition without changing gear freshness");
   using var blocked=new ManualResetEventSlim();using var entered=new ManualResetEventSlim();
   service.TryEvent(Event("slow"),_=>{entered.Set();blocked.Wait();});
   check(entered.Wait(3000),"Writer stall fixture entered");
   check(!service.Flush(1),"Flush may time out safely");blocked.Set();
   check(service.Flush()&&JObject.FromObject(service.State("review",all))["health"]!["storageError"]!.Type==JTokenType.Null,"Late flush barrier does not throw after timeout");
  }
  using(var restored=new SagaStore(servicePath)) {
   check(restored.Events("review",all).Any(e=>e.Id=="accepted")&&restored.Cells("review",new(){"p"}).Count==1,"Acknowledged event and exploration survive restart");
  }
  var backup=Path.Combine(root,"backup-copy");Directory.CreateDirectory(backup);foreach(var file in Directory.GetFiles(servicePath))File.Copy(file,Path.Combine(backup,Path.GetFileName(file)));
  using(var restored=new SagaStore(backup))check(restored.Events("review",all).Any(e=>e.Id=="accepted")&&restored.Cells("review",new(){"p"}).Count==1,"Stopped-directory backup restores statistics and exploration");
  string lorePath=Path.Combine(root,"review-lore");
  using(var service=new SagaService(new(){DataDirectory=lorePath,ListenPrefix="",LoreCooldownMinutes=0,LoreMilestoneEvents=1,LoreEnabled=true,OpenRouterKey="synthetic-only"},new HttpClient(new SyntheticLoreHandler()))) {
   service.Store.Player(new(){World="review",PlayerId="p",Name="Astrid",ShareProfile=true});
   for(int i=0;i<101;i++)service.Store.AddEvent(Event("same-time-"+i.ToString("D3")));
   service.Store.QueueLore("review","p");service.Start();
   await Until(()=>service.Store.Chapters("review").SelectMany(c=>c.EventIds).Distinct().Count()==101);
   var chapters=service.Store.Chapters("review");
   check(chapters.Count==2&&chapters.Sum(c=>c.EventIds.Count)==101,"Lore chunk boundary preserves all 101 same-timestamp events");
   var late=Event("late-arrival");late.Utc=past.AddMinutes(-5);service.TryEvent(late);service.Flush();
   await Until(()=>service.Store.Chapters("review").Any(c=>c.EventIds.Contains("late-arrival")));
   check(service.Store.Chapters("review").SelectMany(c=>c.EventIds).Distinct().Count()==102,"Late-arriving earlier milestone gets a persistent chapter");
  }
 }
 static async Task Until(Func<bool> condition) {var until=DateTime.UtcNow.AddSeconds(25);while(!condition()){if(DateTime.UtcNow>until)throw new Exception("Lore worker fixture timed out");await Task.Delay(50);}}
}
