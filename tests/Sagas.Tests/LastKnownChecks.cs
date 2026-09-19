using Newtonsoft.Json.Linq;
using ValheimSagas;
static class LastKnownChecks {
 public static void Run(string root,Action<bool,string> check){
  var dir=Path.Combine(root,"last-known");var all=new TimeWindow(DateTime.MinValue,DateTime.MaxValue);
  JObject Player(SagaService service)=>JObject.FromObject(service.State("world-id",all))["players"]!.First()!.ToObject<JObject>()!;
  var options=new SagaOptions{DataDirectory=dir,World="world-id",WorldName="Synthetic Mistshore",ListenPrefix="",LoreMilestoneEvents=99999};
  DateTime positionUtc;
  using(var service=new SagaService(options)){
   service.Start();service.UpdatePlayer(new(){World="world-id",PlayerId="p",SharePosition=true,Online=true,X=-100,Z=40});service.Flush();
   var detailed=Convert.ToBase64String(new byte[TerrainTile.RenderedByteCount]);
   check(service.Explore(new(){World="world-id",PlayerId="p",Cells=Enumerable.Range(0,4).Select(i=>new MapCell{X=i,TerrainPixels=detailed}).ToList()}),"Four rendered tiles fit bounded terrain batch");
   check(!service.Explore(new(){World="world-id",PlayerId="p",Cells=Enumerable.Range(0,5).Select(i=>new MapCell{X=i,TerrainPixels=detailed}).ToList()}),"Encoded terrain batch beyond 96k characters rejected");
   check(service.Explore(new(){World="world-id",PlayerId="p",Cells=Enumerable.Range(0,1000).Select(i=>new MapCell{X=i%100,Z=i/100}).ToList()}),"Legacy 1000-cell exploration remains accepted");
   service.Flush();
   var live=Player(service);positionUtc=(DateTime)live["PositionUtc"]!;
   check((bool)live["PositionLive"]!&&(float)live["X"]! == -100,"Fresh consenting position marked live");
   service.SetOffline("world-id","p");service.Flush();var offline=Player(service);
   check(!(bool)offline["Online"]!&&!(bool)offline["PositionLive"]!&&(float)offline["X"]! == -100&&(DateTime)offline["PositionUtc"]! == positionUtc,"Offline marker retains original consenting position timestamp");
   var state=JObject.FromObject(service.State("world-id",all));check((string?)state["worldNames"]!["world-id"]=="Synthetic Mistshore"&&state["worlds"]!.Values<string>().Contains("world-id"),"World labels coexist with stable world identifiers");
  }
  options.WorldName="";
  using(var service=new SagaService(options)){
   service.Start();var restored=Player(service);
   check(!(bool)restored["Online"]!&&!(bool)restored["PositionLive"]!&&(float)restored["X"]! == -100&&(DateTime)restored["PositionUtc"]! == positionUtc,"Restart preserves last-known position without live claim");
   check((string?)JObject.FromObject(service.State("world-id",all))["worldNames"]!["world-id"]=="Synthetic Mistshore","World name persists across empty-name restart");
   var old=service.Store.Players("world-id").Single();old.PositionUtc=DateTime.UtcNow.AddHours(-2);old.Utc=old.PositionUtc.Value;service.Store.Player(old);service.TouchPresence("world-id","p","Viking",true);service.Flush();var stale=Player(service);
   check((bool)stale["Online"]!&&!(bool)stale["PositionLive"]!&&(float)stale["X"]! == -100,"Online heartbeat does not make stale coordinates live");
   service.TouchPresence("world-id","p","Viking",false);service.Flush();var revoked=Player(service);
   check(revoked["X"]!.Type==JTokenType.Null&&revoked["Z"]!.Type==JTokenType.Null&&revoked["PositionUtc"]!.Type==JTokenType.Null&&!service.Store.Players("world-id").Single().X.HasValue,"Position consent revocation removes both public and stored coordinates");
   service.TouchPresence("world-id","p","Viking",true);service.Flush();check(Player(service)["X"]!.Type==JTokenType.Null,"Re-enabling public toggle cannot revive previously revoked coordinates");
   service.UpdatePlayer(new(){World="world-id",PlayerId="p",SharePosition=false,Online=true,X=999,Z=999});service.Flush();check(Player(service)["X"]!.Type==JTokenType.Null&&!service.Store.Players("world-id").Single().X.HasValue,"Nonconsenting snapshot cannot persist supplied coordinates");
  }
 }
}
