using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValheimSagas;

static class ContextChecks {
 public static async Task Run(Action<bool,string> check) {
  var directory=Path.Combine(Path.GetTempPath(),"sagas-context-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(directory);
  try {
   using var service=new SagaService(new SagaOptions{DataDirectory=directory,World="secret-world-context"});var store=service.Store;
   var astrid=new PlayerSnapshot{World="secret-world-context",PlayerId="secret-astrid-context",Name="Astrid",ShareProfile=true,ShareMap=true,Gold=87,EpicLootInstalled=true,EffectiveStats=new(){{"Armor",72}},EffectiveResistances=new(){{"Frost","Resistant"}},Gear=new(){new GearItem{Name="Northern blade",Slot="Main hand",Equipped=true,Rarity="Epic",Quality=4,Stats=new(){{"Slash",40}},Effects=new(){"Frost +12"}}}};
   var bjorn=new PlayerSnapshot{World=astrid.World,PlayerId="secret-bjorn-context",Name="Bjorn",ShareProfile=true};
   var hidden=new PlayerSnapshot{World=astrid.World,PlayerId="secret-hidden-context",Name="Forbidden companion",ShareProfile=false,ShareMap=true,Gold=777777};
   foreach(var p in new[]{astrid,bjorn,hidden})store.Player(p);
   store.Explore(new ExplorationBatch{World=astrid.World,PlayerId=astrid.PlayerId,Cells=new(){new MapCell{X=12345,Z=23456,Biome="Mountain"}}});
   store.Explore(new ExplorationBatch{World=astrid.World,PlayerId=hidden.PlayerId,Cells=new(){new MapCell{X=12345,Z=23456,Biome="HiddenBiome"}}});
   var boss=new SagaEvent{Id="secret-boss-context",World=astrid.World,PlayerId=bjorn.PlayerId,PlayerName="Bjorn",Name="Bonemass",Boss=true,Kind="kill",Contributors=new(){astrid.PlayerId,hidden.PlayerId},DurationSeconds=123};store.AddEvent(boss);
   var bounty=new SagaEvent{Id="secret-bounty-context",World=astrid.World,PlayerId=astrid.PlayerId,PlayerName="Astrid",Kind="bounty",Name="Hunted troll"};store.AddEvent(bounty);
   var context=store.NarrativeContext(astrid.World,astrid.PlayerId);var contextText=string.Join("\n",context.Facts);
   check(contextText.Contains("Northern blade")&&contextText.Contains("Frost +12")&&contextText.Contains("Armor=72")&&contextText.Contains("Frost=Resistant"),"Lore includes current gear, effects, effective stats and resistances");
   check(contextText.Contains("last reported snapshot")&&contextText.Contains("not permanent traits")&&contextText.Contains("carried Coins balance 87"),"Snapshots distinguish current gear and carried balance from career claims");
   check(contextText.Contains("Mountain")&&contextText.Contains("64m cells")&&!contextText.Contains("12345")&&!contextText.Contains("23456"),"Exploration context contains aggregate area/biomes, never cells or coordinates");
   check(contextText.Contains("bounty completions: 1")&&contextText.Contains("Hunted troll")&&contextText.Contains("Bonemass")&&contextText.Contains("Astrid, Bjorn")&&contextText.Contains("not proof of a solo victory"),"Bounties and contributor-only boss credit included without claiming solo");
   check(!contextText.Contains("Forbidden companion")&&!contextText.Contains("HiddenBiome")&&!contextText.Contains("777777"),"Private profile context and map excluded");
   check(store.NarrativeContext(astrid.World,hidden.PlayerId).Facts.Count==0,"Cannot build private character context");
   var handler=new FakeHandler();handler.Responses.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
   var options=new SagaOptions{LoreEnabled=true,OpenRouterKey="test-key-context-secret",LoreModel="openrouter/free"};
   var chapter=await new LoreEngine(options,new HttpClient(handler),()=>true).GenerateAsync("Astrid",new[]{bounty},null,default,new[]{bounty},context);
   var request=handler.Bodies.Single();var data=JObject.Parse((string)JObject.Parse(request)["messages"]![1]!["content"]!);
   check(!new[]{astrid.World,astrid.PlayerId,bjorn.PlayerId,hidden.PlayerId,boss.Id,bounty.Id,options.OpenRouterKey,"12345","23456"}.Any(request.Contains),"Expanded API prompt redacts all identities, keys and coordinates");
   check(data["supportingContext"]!.Any(x=>x.ToString().Contains("Northern blade"))&&(int?)data["retainedCareer"]?["recordedBounties"]==1,"Queued API receives rich factual context and bounty totals");
   check(chapter.Facts.Any(x=>x.Contains("last reported snapshot"))&&chapter.MapParticipants.Contains(astrid.PlayerId)&&chapter.Participants.Any(p=>p.PlayerId==bjorn.PlayerId),"Local fallback preserves auditable context and privacy dependencies");
   check(chapter.Text=="","Provider failure does not invent a template story");chapter.Model="fixture/generated";chapter.Text="Synthetic persisted AI chapter for consent projection testing.";
   chapter.CharacterBio="Legacy template biography";store.Chapter(chapter);
   JObject State()=>JObject.FromObject(service.State(astrid.World,new TimeWindow(DateTime.MinValue,DateTime.UtcNow)));
   check((string?)State()["chapters"]![0]!["CharacterBio"]==""&&store.Chapters(astrid.World).Single().CharacterBio=="Legacy template biography","Legacy template biography hidden without modifying stored history");
   check(State()["chapters"]!.Count()==1,"Context chapter visible with current consent");
   astrid.ShareMap=false;store.Player(astrid);
   check(State()["chapters"]!.Count()==0,"Withdrawing map sharing hides persisted context dependent on its discoveries");
   check(!string.Join("\n",store.NarrativeContext(astrid.World,astrid.PlayerId).Facts).Contains("Mountain"),"Revoked map is absent from newly built context");
   astrid.ShareMap=true;store.Player(astrid);bjorn.ShareProfile=false;store.Player(bjorn);
   check(State()["chapters"]!.Count()==0,"Withdrawing companion profile sharing hides personal chapter that names companion");
   var serverContext=store.NarrativeContext(astrid.World);check(!serverContext.Participants.Any(p=>p.PlayerId==bjorn.PlayerId),"Server context excludes revoked companions");
   var oversized=new LoreContext();oversized.Facts.AddRange(Enumerable.Range(0,200).Select(i=>new string('x',3000)+i));
   handler.Responses.Enqueue(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
   await new LoreEngine(options,new HttpClient(handler),()=>true).GenerateAsync("Astrid",new[]{bounty},null,default,null,oversized);
   var bounded=JObject.Parse((string)JObject.Parse(handler.Bodies.Last())["messages"]![1]!["content"]!);
   check(bounded["supportingContext"]!.Count()==64&&bounded["supportingContext"]!.All(x=>x.ToString().Length<=900)&&(int?)bounded["supportingContextOmittedCount"]==136,"Rich prompt context bounded to64 entries of900 chars with omission count");
   check(handler.Bodies.Last().Length<110000,"Overall expanded synthetic request stays bounded");
  }finally{Directory.Delete(directory,true);}
 }
}
