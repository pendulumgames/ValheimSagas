using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ValheimSagas;

static class SlsChecks {
 static JObject Json(object value)=>JObject.Parse(JsonConvert.SerializeObject(value,new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 public static void Run(string root,Action<bool,string> check){
  var path=Path.Combine(root,"sls-zones");var now=DateTime.UtcNow;var all=new TimeWindow(DateTime.MinValue,now.AddMinutes(1));
  var options=new SagaOptions{DataDirectory=path,World="sls",ListenPrefix="",SlsInstalled=true};
  SlsZone Zone(int x,int level)=>new(){MinX=x*64,MaxX=(x+1)*64,MinZ=0,MaxZ=64,Level=level,Color="#bbaa44"};
  SlsWorldState Snapshot()=>new(){Installed=true,NemesisEnabled=true,ZoneScalingEnabled=true,OverlayEnabled=true,AboveFog=false,Opacity=.42f,OutlineInset=7,Zones=new(){Zone(0,2),Zone(1,3),Zone(2,4),Zone(3,5),Zone(4,6)}};
  string Tile(bool known){var bytes=new byte[TerrainTile.RenderedByteCount];if(known){bytes[0]=80;bytes[1]=90;bytes[2]=20;bytes[3]=255;}return Convert.ToBase64String(bytes);}
  using(var service=new SagaService(options)){
   service.Start();
   check(service.UpdatePlayer(new(){World="sls",PlayerId="a",Name="Astrid",ShareMap=true,NemesisScore=12.5f})&&service.UpdatePlayer(new(){World="sls",PlayerId="b",Name="Bjorn",ShareMap=true,NemesisScore=0})&&service.UpdatePlayer(new(){World="sls",PlayerId="hidden",Name="Hidden",ShareMap=false,ShareProfile=false,NemesisScore=555}),"SLS optional scores accept fractional and zero values in valid snapshots");
   check(service.Explore(new(){World="sls",PlayerId="a",Cells=new(){new(){X=0,TerrainPixels=Tile(true)},new(){X=1,TerrainPixels=Tile(false)}}})&&service.Explore(new(){World="sls",PlayerId="b",Cells=new(){new(){X=4}}})&&service.Explore(new(){World="sls",PlayerId="hidden",Cells=new(){new(){X=2}}}),"Synthetic explored, transparent and private cells accepted");
   var snapshot=Snapshot();check(service.UpdateSls("sls",snapshot),"Server SLS settings snapshot queues successfully");snapshot.Zones[0].Level=999;snapshot.Opacity=.99f;
   check(service.Flush(),"SLS updates and exploration commit");
   JObject Map(HashSet<string>? selected=null)=>Json(service.Map("sls",selected));
   var map=Map();check((int)map["sls"]!["zones"]![0]!["level"]! ==2&&Math.Abs((float)map["sls"]!["opacity"]!-.42f)<.001f&&(float)map["sls"]!["outlineInset"]! ==7,"Queued SLS snapshot copies host values and ignores later caller mutations");
   check(map["sls"]!["zones"]!.Count()==2&&map["sls"]!["zones"]!.Select(z=>(int)z["level"]!).Order().SequenceEqual(new[]{2,6}),"Below-fog overlay omits fully unexplored, all-transparent and private-only zones");
   check(Map(new(){"a"})["sls"]!["zones"]!.Count()==1&&(int)Map(new(){"b"})["sls"]!["zones"]![0]!["level"]! ==6,"Below-fog zone metadata follows selected shared exploration");
   check(Map(new(){"hidden"})["sls"]!["zones"]!.Count()==0&&Map(new(){"hidden"})["cells"]!.Count()==0,"Selecting private explorer cannot expose zones or terrain");
   check(Json(service.Map("other"))["sls"]!["zones"]!.Count()==0&&Json(service.Map("other"))["cells"]!.Count()==0,"Zone settings and exploration remain world isolated");
   var terrain=map["cells"]!.ToString();snapshot=Snapshot();snapshot.AboveFog=true;service.UpdateSls("sls",snapshot);service.Flush();
   map=Map();check(map["sls"]!["zones"]!.Count()==5&&(bool)map["sls"]!["aboveFog"]!,"Above-fog host choice intentionally exposes every configured zone level");
   check(map["cells"]!.ToString()==terrain&&Map(new(){"hidden"})["cells"]!.Count()==0&&Map(new(){"hidden"})["sls"]!["zones"]!.Count()==5,"Above-fog overlay does not export additional terrain or private exploration");
   var state=Json(service.State("sls",all));
   JToken Player(JObject s,string id)=>s["players"]!.Single(p=>(string?)p["playerId"]==id);
   check((float)Player(state,"a")["nemesisScore"]! ==12.5f&&(float)Player(state,"b")["nemesisScore"]! ==0&&Player(state,"hidden")["nemesisScore"]!.Type==JTokenType.Null,"State exposes exact consenting score and redacts private score");
   foreach(var score in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
    check(service.UpdatePlayer(new(){World="sls",PlayerId="invalid",Name="Invalid",NemesisScore=score}),"Nonfinite optional score does not discard otherwise valid character snapshot");service.Flush();check(Player(Json(service.State("sls",all)),"invalid")["nemesisScore"]!.Type==JTokenType.Null,"Nonfinite Nemesis score is sanitized to unavailable");
   }
   service.Store.AddEvent(new(){Id="team-nemesis",World="sls",PlayerId="b",Kind="kill",NemesisBoss=true,Contributors=new(){"a","a","hidden"},Utc=now.AddMinutes(-5)});
   service.Store.AddEvent(new(){Id="private-nemesis",World="sls",PlayerId="hidden",Kind="kill",NemesisBoss=true,Utc=now.AddMinutes(-5)});
   check((int)Json(service.State("sls",all,new(){"a"}))["stats"]!["nemesisBossKills"]! ==1,"Selected contributor receives Nemesis metric despite another player finishing");
   check((int)Json(service.State("sls",all))["stats"]!["nemesisBossKills"]! ==1&&(int)Json(service.State("sls",all,new(){"hidden"}))["stats"]!["nemesisBossKills"]! ==0,"World Nemesis metric counts each shared encounter once and excludes private-only credit");
   check((int)Json(service.State("sls",new TimeWindow(now.AddMinutes(-1),now)))["stats"]!["nemesisBossKills"]! ==0,"Nemesis state metric obeys selected time window");
   snapshot=Snapshot();snapshot.OverlayEnabled=false;service.UpdateSls("sls",snapshot);service.Flush();check(Map()["sls"]!["zones"]!.Count()==0&&service.Store.Sls("sls").Zones.Count==0,"Disabled overlay removes zone payload and persisted zone geometry");
   snapshot=Snapshot();snapshot.ZoneScalingEnabled=false;service.UpdateSls("sls",snapshot);service.Flush();check(Map()["sls"]!["zones"]!.Count()==0,"Disabled zone scaling renders no overlay");
   snapshot=Snapshot();snapshot.Installed=false;service.UpdateSls("sls",snapshot);service.Flush();state=Json(service.State("sls",all));check(!(bool)state["sls"]!["installed"]!&&!(bool)state["sls"]!["nemesisEnabled"]!&&Player(state,"a")["nemesisScore"]!.Type==JTokenType.Null&&Map()["sls"]!["zones"]!.Count()==0,"Absent SLS normalizes flags and hides optional score and zones");
   foreach(var opacity in new[]{-.1f,1.1f,float.NaN,float.PositiveInfinity}){snapshot=Snapshot();snapshot.Opacity=opacity;check(!service.UpdateSls("sls",snapshot),"Invalid SLS opacity rejected");}
   foreach(var bounds in new[]{(5f,4f),(0f,0f),(float.NaN,64f),(0f,float.PositiveInfinity),(-20001f,0f)}){snapshot=Snapshot();snapshot.Zones[0].MinX=bounds.Item1;snapshot.Zones[0].MaxX=bounds.Item2;check(!service.UpdateSls("sls",snapshot),"Malformed or out-of-world zone bounds rejected");}
   snapshot=Snapshot();snapshot.Zones[0].Level=-1;check(!service.UpdateSls("sls",snapshot),"Negative zone level rejected");snapshot=Snapshot();snapshot.Zones[0].Color="url(https://invalid)";check(!service.UpdateSls("sls",snapshot),"Non-color zone style rejected");
   service.UpdateSls("sls",Snapshot());service.Flush();
  }
  using(var service=new SagaService(options)){var map=Json(service.Map("sls"));check((bool)map["sls"]!["installed"]!&&map["sls"]!["zones"]!.Count()==2&&service.Store.Sls("sls").Zones.Count==5,"SLS host settings and exploration-aware zone projection survive restart");}
  options.SlsInstalled=false;
  using(var service=new SagaService(options)){var map=Json(service.Map("sls"));var state=Json(service.State("sls",all));check(!(bool)map["sls"]!["installed"]!&&map["sls"]!["zones"]!.Count()==0&&state["players"]!.All(p=>p["nemesisScore"]!.Type==JTokenType.Null),"Server runtime absence overrides stale persisted SLS flags and scores");check(!(bool)Json(service.Leaderboard("sls",all))["availability"]!["nemesisTrackingAvailable"]!,"Runtime mod absence disables optional leaderboard availability despite old records");}
 }
}
