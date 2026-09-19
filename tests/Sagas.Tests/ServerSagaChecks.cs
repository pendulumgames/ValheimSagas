using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValheimSagas;

static class ServerSagaChecks {
 public static async Task Run(string root,Action<bool,string> check) {
  var directory=Path.Combine(root,"server-saga");var world="shared-world";var stamp=DateTime.UtcNow.AddDays(-2);
  SagaEvent Event(string id,string player,string name)=>new(){Id=id,World=world,PlayerId=player,PlayerName=name,Kind="kill",Name="Troll",Prefab="Troll",Stars=3,Utc=stamp};
  var a=Event("a-event","a","Astrid");var b=Event("b-event","b","Bjorn");var secret=Event("private-event","private","Private name");
  // Old recorded history, without any new player job, must bootstrap the actual server worker.
  using(var store=new SagaStore(directory)) {
   foreach(var p in new[]{new PlayerSnapshot{World=world,PlayerId="a",Name="Astrid",ShareProfile=true},new(){World=world,PlayerId="b",Name="Bjorn",ShareProfile=true},new(){World=world,PlayerId="private",Name="Private name",ShareProfile=false}})store.Player(p);
   store.AddEvent(a);store.AddEvent(b);store.AddEvent(secret);
   var foreign=Event("foreign-event","a","Foreign name");foreign.World="another-world";store.AddEvent(foreign);
  }
  var options=new SagaOptions{DataDirectory=directory,ListenPrefix="",World=world,ServerName="Northern Hall",WorldName="Midgard",LoreCooldownMinutes=0,LoreMilestoneEvents=1};
  string initialJson;
  using(var service=new SagaService(options,new HttpClient(new SyntheticLoreHandler()))) {
   options.LoreEnabled=true;options.OpenRouterKey="synthetic-only";service.Start();await Until(()=>service.Store.ServerChapters(world).Count==1);
   var chapter=service.Store.ServerChapters(world).Single();initialJson=JsonConvert.SerializeObject(chapter);
   check(chapter.Scope=="server"&&chapter.EventIds.Count==2&&chapter.Participants.Count==2,"Startup generates real shared chapter from consented retained history");
   check(chapter.Facts.All(f=>!f.Contains("Private name"))&&!chapter.EventIds.Contains("foreign-event"),"Server generation isolates world and private profiles");
   check(chapter.ServerBio.StartsWith("Synthetic fellowship")&&chapter.Participants.Any(p=>p.Name=="Astrid")&&chapter.Participants.Any(p=>p.Name=="Bjorn"),"Generated server biography retains named factual participants");
   check(service.Store.Chapters(world).Count==0,"Shared generation does not create or rewrite personal chapters");
   var state=JObject.FromObject(service.State(world,new TimeWindow(DateTime.MinValue,DateTime.UtcNow)));
   check(state["chapters"]!.Count()==0,"Personal state endpoint does not merge shared chapters");
   var response=JObject.FromObject(service.ServerSaga(world));
   check(response["chapters"]!.Count()==1&&(string?)response["bio"]==chapter.ServerBio&&(string?)response["server"]?["name"]=="Northern Hall","Shared API exposes persisted biography and chapter");
   check(JObject.FromObject(service.ServerSaga("another-world"))["chapters"]!.Count()==0,"Another world's API cannot retrieve shared chapter");
   var duplicate=JsonConvert.DeserializeObject<SagaChapter>(initialJson)!;duplicate.Text="MUTATED";service.Store.Chapter(duplicate);
   check(JsonConvert.SerializeObject(service.Store.ServerChapters(world).Single())==initialJson,"Shared chapter insert is immutable under repeated id");
   var newB=Event("b-later","b","Bjorn");newB.Utc=stamp.AddDays(1);service.Store.AddEvent(newB);service.Store.QueueServerLore(world);
   await Until(()=>service.Store.ServerChapters(world).Count==2);
   var later=service.Store.ServerChapters(world).Last();
   check(later.EventIds.SequenceEqual(new[]{"b-later"})&&later.Participants.Any(p=>p.PlayerId=="a"),"Continuity preserves prior participant privacy dependency with B-only new facts");
   check(service.Store.ServerChapters(world).SelectMany(c=>c.EventIds).Distinct().Count()==3,"Shared event coverage deduplicates earlier facts");
   var revoked=service.Store.Players(world).Single(p=>p.PlayerId=="a");revoked.ShareProfile=false;service.Store.Player(revoked);
   var hidden=JsonConvert.SerializeObject(service.ServerSaga(world));var hiddenObject=JObject.Parse(hidden);
   check(hiddenObject["chapters"]!.Count()==0&&(string?)hiddenObject["bio"]==""&&hiddenObject["participants"]!.Count()==0&&!hidden.Contains("Astrid"),"Revocation hides original and continuity-dependent prose, bio, names and facts");
   check(service.Store.ServerChapters(world).Count==2,"Consent revocation does not erase persisted chapter history");
   var postRevocation=Event("b-after-revocation","b","Bjorn");postRevocation.Utc=stamp.AddDays(1).AddMinutes(1);service.Store.AddEvent(postRevocation);service.Store.QueueServerLore(world);
   await Until(()=>service.Store.ServerChapters(world).Count==3);
   var independent=service.Store.ServerChapters(world).Last();
   check(independent.Participants.All(p=>p.PlayerId=="b")&&!independent.Text.Contains("Astrid")&&!independent.ServerBio.Contains("Astrid"),"New generation excludes revoked career and hidden previous context");
   check(JObject.FromObject(service.ServerSaga(world))["chapters"]!.Count()==1,"Only newly independent consenting chapter is visible");
  }
  using(var store=new SagaStore(directory)) {
   check(store.ServerChapters(world).Count==3&&store.PendingServerLore().Contains(world),"Shared chapters and durable world queue survive restart");
   check(JsonConvert.SerializeObject(store.ServerChapters(world).First())==initialJson,"Earlier shared chapter remains byte-equivalent after later chapters and restart");
   check(store.ServerChapters("another-world").Count==0,"Persisted shared chapter world isolation");
  }
  using(var service=new SagaService(options,new HttpClient(new SyntheticLoreHandler()))) {
   check(JObject.FromObject(service.ServerSaga(world))["chapters"]!.Count()==1,"Revoked consent continues to hide dependent chapters after restart");
   var reconsent=service.Store.Players(world).Single(p=>p.PlayerId=="a");reconsent.ShareProfile=true;service.Store.Player(reconsent);
   check(JObject.FromObject(service.ServerSaga(world))["chapters"]!.Count()==3,"Reconsent exposes preserved history without rewriting or regeneration");
  }
 }
 static async Task Until(Func<bool> condition){var deadline=DateTime.UtcNow.AddSeconds(25);while(!condition()){if(DateTime.UtcNow>deadline)throw new Exception("Shared lore worker fixture timed out");await Task.Delay(40);}}
}
