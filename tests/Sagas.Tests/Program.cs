using ValheimSagas;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;
var root=Path.Combine(Path.GetTempPath(),"sagas-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);int assertions=0;
void Check(bool yes,string message){assertions++;if(Environment.GetEnvironmentVariable("SAGAS_TEST_TRACE")=="1")Console.WriteLine("CHECK "+assertions+": "+message+" = "+yes);if(!yes)throw new Exception(message);}
foreach(var invalid in new[]{"", "ftp://example.com", "javascript:alert(1)", "file:///C:/secret", "http://user:secret@example.com/", "https://example.com/?token=secret", "http://example.com/#secret", "http://*:8877/", "http://0.0.0.0:8877/", "http://[::]:8877/", new string('x',2049)})Check(WebsiteAddress.Validate(invalid)=="","Unsafe/non-browsable website address rejected");
Check(WebsiteAddress.Validate(" http://127.0.0.1:8877/ ")=="http://127.0.0.1:8877/","Local website accepted");
Check(WebsiteAddress.Validate("https://example.com/sagas/")=="https://example.com/sagas/","Proxy subpath accepted");
if(args.Contains("--website-only")){Console.WriteLine($"PASS {assertions} website URL assertions.");Directory.Delete(root,true);return;}
if(args.Contains("--jewel-only")){await JewelcraftingChecks.Run(root,Check);Console.WriteLine($"PASS {assertions} website and Jewelcrafting assertions.");Directory.Delete(root,true);return;}
if(args.Contains("--north-only")){NorthBossChecks.Run(root,Check);Console.WriteLine($"PASS {assertions} North boss and directory metadata assertions.");Directory.Delete(root,true);return;}
if(args.Contains("--offline-media-only")){await OfflineMediaChecks.Run(root,Check);Console.WriteLine($"PASS {assertions} deferred/offline portrait assertions.");Directory.Delete(root,true);return;}
AnalyticsDeliveryChecks.Run(root,Check);
if(args.Contains("--analytics-only")){Console.WriteLine($"PASS {assertions} analytics delivery checks.");Directory.Delete(root,true);return;}
AdventureChecks.Run(root,Check);JourneyChecks.Run(root,Check);
if(args.Contains("--adventures-only")){Console.WriteLine($"PASS {assertions} adventure assertions.");Directory.Delete(root,true);return;}
WebsiteSetupChecks.Run(Check);
if(args.Contains("--website-setup-only")){Console.WriteLine($"PASS {assertions} website setup checks.");Directory.Delete(root,true);return;}
if(args.Contains("--map-details-only")){MapDetailsChecks.Run(root,Check);await MapDetailsHttpChecks.Run(root,Check);VikingMapChecks.Run(root,Check);Console.WriteLine($"PASS {assertions} map detail assertions.");Directory.Delete(root,true);return;}
if(args.Contains("--sls-only")){SlsChecks.Run(root,Check);Console.WriteLine($"PASS {assertions} SLS assertions.");Directory.Delete(root,true);return;}
if(args.Contains("--nemesis-only")){NemesisChecks.Run(root,Check);Console.WriteLine($"PASS {assertions} Nemesis assertions.");Directory.Delete(root,true);return;}
if(args.Contains("--personal-lore-only")){await PersonalLoreChecks.Run(root,Check);Console.WriteLine($"PASS {assertions} personal storyteller assertions.");Directory.Delete(root,true);return;}
if(args.Contains("--player-login-only")){await PlayerLoginChecks.Run(root,Check);Console.WriteLine($"PASS {assertions} player login assertions.");Directory.Delete(root,true);return;}
if(args.Contains("--profile-biomes-only")){await ProfileBiomeChecks.Run(root,Check);Console.WriteLine($"PASS {assertions} profile biome and static-route assertions.");Directory.Delete(root,true);return;}
if(args.Contains("--media-only")){await MediaTransferChecks.Run(root,Check);Console.WriteLine($"PASS {assertions} high resolution media assertions.");Directory.Delete(root,true);return;}
if(args.Contains("--network-only")){NetworkReliabilityChecks.Run(Check);TelemetryBudgetChecks.Run(Check);Console.WriteLine($"PASS {assertions} network assertions.");Directory.Delete(root,true);return;}
VersionPolicyChecks.Run(Check);
if(args.Contains("--version-only")){Console.WriteLine($"PASS {assertions} version assertions.");Directory.Delete(root,true);return;}
MapDeliveryChecks.Run(root,Check);
if(args.Contains("--map-delivery-only")){Console.WriteLine($"PASS {assertions} map delivery assertions.");Directory.Delete(root,true);return;}
LootMetadataChecks.Run(root,Check);
SlsCaptureChecks.Run(Check);
TelemetryBudgetChecks.Run(Check);
NetworkReliabilityChecks.Run(Check);
await HostPaidLoreChecks.Run(root,Check);
SagaEvent Event(string id,string kind="kill",int amount=1,string provenance="")=>new(){Id=id,World="world",PlayerId="p1",PlayerName="Astrid",Utc=new DateTime(2026,9,17,10,0,0,DateTimeKind.Utc),Kind=kind,Name="Troll",Prefab="Troll",Amount=amount,Provenance=provenance,Stars=2,X=32,Z=32};
var now=new DateTime(2026,9,17,12,0,0,DateTimeKind.Utc);
foreach(var pair in new Dictionary<string,int>{{"30m",30},{"1h",60},{"6h",360},{"12h",720},{"1d",1440},{"3d",4320},{"7d",10080}}){var w=TimeWindow.Parse(pair.Key,null,null,now);Check(w.From==now.AddMinutes(-pair.Value),pair.Key+" duration");Check(w.Contains(w.From)&&w.Contains(now)&&!w.Contains(w.From.AddTicks(-1))&&!w.Contains(now.AddTicks(1)),pair.Key+" inclusive boundaries");}
Check(TimeWindow.Parse("all",null,null,now).From==DateTime.MinValue,"All history");Check(TimeWindow.Parse("custom","2026-09-17T05:00:00-06:00","2026-09-17T12:00:00Z",now).From==now.AddHours(-1),"UTC offset");
try{TimeWindow.Parse("custom","bad","bad",now);throw new Exception("Invalid dates accepted");}catch(ArgumentException){assertions++;}
var all=new TimeWindow(DateTime.MinValue,DateTime.MaxValue);
using(var s=new SagaStore(root)){
 Check(s.AddEvent(Event("death-zdo")),"Insert kill");Check(!s.AddEvent(Event("death-zdo")),"Duplicate death");
 Check(s.AddEvent(Event("drop-item","drop",5,"item")),"Drop");Check(s.AddEvent(Event("pickup-part1","collect",2,"item")),"Partial pickup");Check(s.AddEvent(Event("pickup-part2","collect",7,"item")),"Cap overcollection");Check(!s.AddEvent(Event("pickup-repeat","collect",5,"item")),"Repeat pickup blocked");Check(s.AddEvent(Event("unknown","collect",5,"missing")),"Unknown provenance durably pending, not earned loot");
 Check(s.Events("world",all).Where(e=>e.Kind=="collect").Sum(e=>e.Amount)==5,"Earned cap");Check(s.Events("other",all).Count==0,"World isolation");
 s.Player(new(){World="world",PlayerId="p1",Name="Astrid",Online=true,ShareMap=true,SharePosition=true,X=32,Z=32});s.Player(new(){World="world",PlayerId="p2",Name="Private",ShareMap=false,ShareProfile=false,X=999,Z=999});
 s.Explore(new(){World="world",PlayerId="p1",Imported=true,Cells=new(){new(){X=0,Z=0,Biome="Meadows"}}});s.Explore(new(){World="world",PlayerId="p2",Cells=new(){new(){X=1,Z=1,Biome="Swamp"}}});
 Check(s.Cells("world",new(){"p1"}).Count==1,"Personal exploration");Check(s.Cells("world",new(){"p1","p2"}).Count==2,"Exploration union");Check(s.ReserveLoreRequest(now,1)&&!s.ReserveLoreRequest(now,1),"Lore budget");s.QueueLore("world","p1");s.Chapter(new(){Id="chapter",World="world",PlayerId="p1",Utc=now,Title="Test",Text="Fiction",Facts=new(){"Recorded Troll"},EventIds=new(){"death-zdo"}});s.Checkpoint();
}
using(var s=new SagaStore(root)){Check(!s.AddEvent(Event("death-zdo")),"Restart dedup");Check(s.Events("world",all).Count==4,"Restart events");Check(s.Players("world").All(p=>!p.Online),"Restart invalidates presence");Check(s.Chapters("world").Count==1&&s.PendingLore().Length==1,"Persistent chapters and queue");Check(!s.ReserveLoreRequest(now,1),"Persistent request budget");}
var web=Path.Combine(root,"web");Directory.CreateDirectory(web);File.WriteAllText(Path.Combine(web,"index.html"),"<h1>Test</h1>");
// OS-selected loopback port, isolated listener; never opens a firewall port.
var portProbe=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);portProbe.Start();int port=((IPEndPoint)portProbe.LocalEndpoint).Port;portProbe.Stop();
using(var s=new SagaService(new(){DataDirectory=root,WebDirectory=web,World="world",RequireViewerToken=true,ViewerToken="test-token-at-least-24-characters",ListenPrefix=$"http://127.0.0.1:{port}/",LoreMilestoneEvents=99999})){
 s.Start();var outside=Event("outside");outside.X=500;outside.Z=500;Check(s.TryEvent(outside),"Queue event");
 Check(s.Flush(),"Queue drained");var state=JObject.FromObject(s.State("world",all));Check((int)state["stats"]!["kills"]! ==2,"Stats full aggregate");
 var map=JObject.FromObject(s.Map("world"));Check(map["cells"]!.Count()==1,"API map excludes private discovery");Check(JObject.FromObject(s.Map("world",new(){"p2"}))["cells"]!.Count()==0,"Cannot request private map by ID");
 var bad=Event("bad");bad.Amount=-1;Check(!s.TryEvent(bad),"Negative amount rejected");bad=Event("nan");bad.X=float.NaN;Check(!s.TryEvent(bad),"NaN rejected");
 using var client=new HttpClient{BaseAddress=new Uri($"http://127.0.0.1:{port}/")};Check((await client.GetAsync("api/state")).StatusCode==HttpStatusCode.Unauthorized,"API auth required");Check((await client.GetAsync("api/map?token=test-token-at-least-24-characters")).StatusCode==HttpStatusCode.Unauthorized,"Query tokens forbidden");client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer","test-token-at-least-24-characters");Check((await client.GetAsync("api/state?range=all")).IsSuccessStatusCode,"Authenticated state");Check((await client.GetAsync("api/state?range=bogus")).StatusCode==HttpStatusCode.BadRequest,"Invalid filter HTTP400");Check((await client.GetAsync("sagas.db")).StatusCode==HttpStatusCode.NotFound,"Database never served");Check((await client.PostAsync("api/state",new StringContent("{}"))).StatusCode==HttpStatusCode.MethodNotAllowed,"Read-only API");
}
await CoreReviewChecks.Run(root,Check);
await DiagnosticsChecks.Run(root,Check);
RetentionChecks.Run(root,Check);
FilterIntegrationChecks.Run(root,Check);
PluginScanChecks.Run(Check);
RenderedTerrainChecks.Run(Check);
RarityColorChecks.Run(root,Check);
await PresentationDataChecks.Run(root,Check);
LastKnownChecks.Run(root,Check);
await PublicAccessChecks.Run(root,Check);
await OfflineMediaChecks.Run(root,Check);
await JewelcraftingChecks.Run(root,Check);
await MediaTransferChecks.Run(root,Check);
await ServerSagaChecks.Run(root,Check);
await LeaderboardChecks.Run(root,Check);NorthBossChecks.Run(root,Check);
NemesisChecks.Run(root,Check);
SlsChecks.Run(root,Check);
MapDetailsChecks.Run(root,Check);await MapDetailsHttpChecks.Run(root,Check);VikingMapChecks.Run(root,Check);
await ProfileBiomeChecks.Run(root,Check);
ProfileLinkChecks.Run(root,Check);
await PlayerLoginChecks.Run(root,Check);
await PersonalLoreChecks.Run(root,Check);
Console.WriteLine($"PASS {assertions} assertions: filters, persistence, provenance, privacy, queue, HTTP auth, restart, lore durability.");
Directory.Delete(root,true);
