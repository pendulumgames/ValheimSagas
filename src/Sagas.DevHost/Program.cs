using ValheimSagas;
// This host is an isolated synthetic fixture harness, never a game-telemetry substitute.
var root=Path.GetFullPath(args.Length>0?args[0]:".dev/fixture");
var token="synthetic-development-token-only";
using var service=new SagaService(new SagaOptions{DataDirectory=root,WebDirectory=Path.GetFullPath("src/Sagas.Web"),ListenPrefix="http://127.0.0.1:9847/",RequireViewerToken=true,ViewerToken=token,World="synthetic-midgard",Synthetic=true,LoreEnabled=true,OpenRouterKey="synthetic-never-sent-to-openrouter",LoreMilestoneEvents=3,LoreCooldownMinutes=1,Log=Console.WriteLine},new HttpClient(new SyntheticLoreHandler()));
service.Start();
var names=new[]{"Astrid Ashwalker","Bjorn of the Pines"};var now=DateTime.UtcNow;
for(int p=0;p<2;p++) {
 var id="fixture-"+p;var gear=new List<GearItem>{new GearItem{Slot="Chest",Name="Wolf armor chest",Type="Chest",Quality=3,Rarity="Rare",Durability=810,MaxDurability=1200,BaseStats=new(){{"Armor",20}},Stats=new(){{"Armor",24},{"Movement modifier",-0.05f}},Effects=new(){"Synthetic example: frost resistance; conditional effect not aggregated"}},new GearItem{Slot="RightHand",Name="Silver sword",Type="OneHandedWeapon",Quality=2,Rarity="Epic",Durability=158,MaxDurability=225,BaseStats=new(){{"Slash",75},{"Spirit",30}},Stats=new(){{"Slash",81},{"Spirit",35}},Effects=new(){"Synthetic example: +10% attack speed"}}};
 service.UpdatePlayer(new PlayerSnapshot{World="synthetic-midgard",PlayerId=id,Name=names[p],Online=true,ShareMap=true,SharePins=true,SharePosition=true,X=p*512-128,Z=p*200, Gear=gear,EffectiveStats=new(){{"Armor",64},{"Max health (food-dependent)",145},{"Max stamina (food-dependent)",170}}});
 var cells=new List<MapCell>();for(int x=-18+p*12;x<12+p*12;x++)for(int z=-15;z<15;z++)if(x*x+z*z<900)cells.Add(new MapCell{X=x,Z=z,Biome=x<-8?"Ocean":z>7?"Mountain":x>10?"Swamp":x>2?"BlackForest":"Meadows",Height=z>7?130:30});
 service.Explore(new ExplorationBatch{World="synthetic-midgard",PlayerId=id,Imported=true,Cells=cells});
 for(int i=0;i<30;i++) {var utc=now.AddMinutes(-i*79-p*7);string eid=$"fixture-{p}-{i}";service.TryEvent(new SagaEvent{Id=eid,World="synthetic-midgard",PlayerId=id,PlayerName=names[p],Utc=utc,Kind="kill",Prefab=i%3==0?"Draugr":"Greydwarf",Name=i%3==0?"Draugr":"Greydwarf",Stars=i%5,Source="synthetic",Biome=i%3==0?"Swamp":"BlackForest",X=i*28-300+p*100,Z=i*13-200});
  service.TryEvent(new SagaEvent{Id=eid+"-drop",Provenance=eid+"-drop",World="synthetic-midgard",PlayerId=id,PlayerName=names[p],Utc=utc.AddSeconds(1),Kind="drop",Prefab="Resin",Name="Resin",Amount=3,Source="synthetic",X=i*28-300+p*100,Z=i*13-200});
  service.TryEvent(new SagaEvent{Id=eid+"-collect",Provenance=eid+"-drop",World="synthetic-midgard",PlayerId=id,PlayerName=names[p],Utc=utc.AddSeconds(2),Kind="collect",Prefab="Resin",Name="Resin",Amount=3,Source="synthetic",X=i*28-300+p*100,Z=i*13-200});
 }
}
// Clearly synthetic map pins and daylight exercise the independent atlas feeds.
service.UpdatePins(new MapPins{World="synthetic-midgard",PlayerId="fixture-0",Revision="fixture-pins-v1",Pins=new(){new(){Name="Synthetic boss altar",Type="Boss",X=-400,Z=160},new(){Name="Synthetic trader",Type="None",X=300,Z=300},new(){Name="Synthetic personal camp",Type="Icon1",X=-200,Z=-300,Personal=true}}});
service.UpdateClock(WorldClock.Sample("synthetic-midgard",42,.4f,1200*42+450,1200,1,true,false));
// A separate, clearly synthetic world keeps the original 60-kill browser fixtures stable.
const string arena="synthetic-leaderboard";
service.Store.WorldName(arena,"Synthetic proving grounds");
var trialNames=new[]{"Astrid of the Trial","Bjorn Shieldbearer","Cora Farwalker"};
for(var i=0;i<3;i++) service.UpdatePlayer(new PlayerSnapshot{World=arena,PlayerId="trial-"+i,Name=trialNames[i],Online=false,ShareProfile=true,EpicLootInstalled=i<2,Gold=i==0?1250:i==1?450:null});
service.UpdatePlayer(new PlayerSnapshot{World=arena,PlayerId="trial-private",Name="Private synthetic Viking",ShareProfile=false,Gold=99999,EpicLootInstalled=true});
void TrialKill(string id,string prefab,string name,int finisher,int[] contributors,double? duration,int stars,double minutes,bool boss=true){service.TryEvent(new SagaEvent{Id="trial-kill-"+id,World=arena,PlayerId="trial-"+finisher,PlayerName=trialNames[finisher],Kind="kill",Prefab=prefab,Name=name,Boss=boss,Stars=stars,DurationSeconds=duration,Contributors=contributors.Select(i=>"trial-"+i).ToList(),Utc=now.AddMinutes(-minutes),Source="synthetic",Biome="Meadows"});}
TrialKill("eikthyr-first","Eikthyr","Eikthyr",0,new[]{0,1},120,0,70);
TrialKill("eikthyr-fast","Eikthyr","Eikthyr",1,new[]{1,2},75,0,5);
TrialKill("elder","gd_king","The Elder",1,new[]{0,1,2},185,0,25);
TrialKill("bonemass-legacy","Bonemass","Bonemass",0,new[]{0,2},null,0,2880);
TrialKill("unranked","SyntheticSeaGuardian","Synthetic Sea Guardian",2,new[]{1,2},90,0,15);
TrialKill("starred","Draugr","Draugr",0,new[]{0},null,4,10,false);
TrialKill("troll","Troll","Troll",1,new[]{1},null,2,20,false);
for(var i=0;i<3;i++){var who=i==2?2:0;var id="trial-loot-"+i;service.TryEvent(new SagaEvent{Id=id+"-drop",Provenance=id,World=arena,PlayerId="trial-"+who,PlayerName=trialNames[who],Kind="drop",Prefab="SyntheticSword",Name="Synthetic enchanted sword",Rarity=i==2?"Rare":"Epic",RarityColor=i==2?"#4a8cff":"#b967e8",Amount=1,Quality=3,Source="synthetic",Utc=now.AddMinutes(-12-i)});service.TryEvent(new SagaEvent{Id=id+"-collect",Provenance=id,World=arena,PlayerId="trial-"+who,PlayerName=trialNames[who],Kind="collect",Prefab="SyntheticSword",Name="Synthetic enchanted sword",Rarity=i==2?"Rare":"Epic",RarityColor=i==2?"#4a8cff":"#b967e8",Amount=1,Quality=3,Source="synthetic",Utc=now.AddMinutes(-11-i)});}
for(var i=0;i<3;i++)service.TryEvent(new SagaEvent{Id="trial-bounty-"+i,World=arena,PlayerId="trial-"+(i==2?1:0),PlayerName=trialNames[i==2?1:0],Kind="bounty",Name="Synthetic bounty completion",Prefab="SyntheticBounty",Amount=1,Source="synthetic",Utc=now.AddMinutes(i==0?-120:-10)});
// Dedicated synthetic login world. Credentials rotate on each DevHost start and are
// written only to the isolated fixture directory, never served or packaged.
const string loginWorld="synthetic-login";
service.Store.WorldName(loginWorld,"Synthetic login trials");
foreach(var id in new[]{"login-champion","login-novice"})service.UpdatePlayer(new PlayerSnapshot{World=loginWorld,PlayerId=id,Name=id=="login-champion"?"Synthetic Champion":"Synthetic Novice",ShareProfile=true});
foreach(var boss in new[]{"Eikthyr","gd_king","Bonemass","Dragon","GoblinKing","SeekerQueen","Fader"})service.TryEvent(new SagaEvent{Id="login-boss-"+boss,World=loginWorld,PlayerId="login-champion",PlayerName="Synthetic Champion",Kind="kill",Boss=true,Prefab=boss,Name=boss,Utc=now.AddMinutes(-5),Source="synthetic"});
service.Flush();
File.WriteAllText(Path.Combine(root,"synthetic-logins.json"),System.Text.Json.JsonSerializer.Serialize(new{world=loginWorld,champion=service.Store.RotatePlayerLogin(loginWorld,"login-champion"),novice=service.Store.RotatePlayerLogin(loginWorld,"login-novice")}));
service.Flush();Console.WriteLine("SYNTHETIC FIXTURE ONLY — http://127.0.0.1:9847/\nMember token: "+token+"\nCtrl+C to stop.");
using var quit=new ManualResetEventSlim();Console.CancelKeyPress+=(s,e)=>{e.Cancel=true;quit.Set();};
while(!quit.Wait(5000)){foreach(var p in service.Store.Players("synthetic-midgard")){p.Online=true;service.UpdatePlayer(p);}}



internal sealed class SyntheticLoreHandler:HttpMessageHandler {
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(new{model="fixture/generated",choices=new[]{new{message=new{content=Newtonsoft.Json.JsonConvert.SerializeObject(new{title="Synthetic recorded chapter",text="Synthetic HTTP test narrative with a named supporting ledger; this is not recorded gameplay or a real model response.",characterBio="Synthetic character biography used only for deterministic automated checks.",serverBio="Synthetic fellowship biography used only for deterministic automated checks."})}}}}))});
}
