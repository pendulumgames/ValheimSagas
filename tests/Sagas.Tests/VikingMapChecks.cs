using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ValheimSagas;
static class VikingMapChecks {
 static JObject Json(object value)=>JObject.Parse(JsonConvert.SerializeObject(value,new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 public static void Run(string root,Action<bool,string> check){
  var options=new SagaOptions{DataDirectory=Path.Combine(root,"viking-colors"),World="w",ListenPrefix=""};string original="";
  using(var s=new SagaService(options)){
   s.Start();for(int i=0;i<10;i++)s.UpdatePlayer(new(){World="w",PlayerId="v"+i,Name="Viking "+i,ShareMap=true,SharePins=true,MapColor="#000000"});s.Flush();
   var players=s.Store.Players("w");var colors=players.Select(p=>p.MapColor).ToArray();original=players.Single(p=>p.PlayerId=="v0").MapColor;
   check(colors.All(VikingColors.Valid)&&colors.Distinct().Count()==10,"Host assigns ten distinct valid colors instead of trusting client input");
   var minimum=Enumerable.Range(0,10).SelectMany(i=>Enumerable.Range(i+1,9-i).Select(j=>VikingColors.Distance(colors[i],colors[j]))).Min();check(minimum>20,"First ten Viking colors maintain perceptual separation: "+minimum.ToString("F1"));
   s.UpdatePlayer(new(){World="w",PlayerId="v0",Name="Renamed",ShareMap=true,SharePins=true,MapColor="#ffffff"});s.TouchPresence("w","v0","Renamed");s.Flush();check(s.Store.Players("w").Single(p=>p.PlayerId=="v0").MapColor==original,"Rename, snapshot and presence preserve server-assigned color");
   foreach(var id in new[]{"v0","v1"}){s.Explore(new(){World="w",PlayerId=id,Cells=new(){new(){X=0,Z=0}}});s.UpdatePins(new(){World="w",PlayerId=id,Revision="one",Pins=new(){new(){Type="Boss",Name="Shared altar",X=2,Z=2},new(){Type="Icon0",Name="Far-away personal pin",X=8000,Z=8000,Personal=true},new(){Type="800",Name="Undiscovered bounty",X=8000,Z=8000}}});}s.Flush();
   var pins=(JArray)Json(s.MapIcons("w"))["pins"]!;check(pins.Count==3,"Game pin deduplicates, personal pins survive outside fog, undiscovered mod locations do not");
   var boss=pins.Single(p=>p["type"]!.Value<string>()=="Boss");check(boss["sources"]!.Count()==2&&boss["sources"]!.All(p=>VikingColors.Valid(p["color"]!.Value<string>())),"Shared marker preserves both owners and colors");
   var selected=(JArray)Json(s.MapIcons("w",new(){"v1"}))["pins"]!;check(selected.Count==2&&selected.Single(p=>p["type"]!.Value<string>()=="Boss")["sources"]!.Count()==1,"Removing first sharer keeps game marker for second owner");
   var revoked=s.Store.Players("w").Single(p=>p.PlayerId=="v0");revoked.SharePins=false;s.UpdatePlayer(revoked);s.Flush();check(((JArray)Json(s.MapIcons("w"))["pins"]!).Count==2,"Outside-fog annotation still requires consent");
  }
  using(var s=new SagaService(options)){s.Start();check(s.Store.Players("w").Single(p=>p.PlayerId=="v0").MapColor==original,"Color survives restart");s.UpdatePlayer(new(){World="other",PlayerId="v0",Name="Elsewhere"});s.Flush();check(VikingColors.Valid(s.Store.Players("other").Single().MapColor),"Other world gets its own assignment");}
  check(MapPinPolicy.Classify(800,false,true,false,false)==1&&MapPinPolicy.Classify(801,false,true,false,false)==1,"Epic Loot bounty/treasure types accepted");
  check(MapPinPolicy.Classify(999,true,true,false,false)==2&&MapPinPolicy.Classify(8,true,true,false,false)==2,"Unknown saved icons require personal consent");
  check(MapPinPolicy.Classify(999,false,false,false,false)==0&&MapPinPolicy.Classify(999,false,true,true,false)==0,"Sprite-less custom and copied icons excluded");
  foreach(var type in new[]{7,10,12,13})check(MapPinPolicy.Classify(type,false,true,false,false)==0,"Chatter/player/area excluded: "+type);
  var crowd=new List<string>();for(int i=0;i<32;i++)crowd.Add(VikingColors.Choose(crowd));check(crowd.Distinct(StringComparer.OrdinalIgnoreCase).Count()==32,"Expanded color candidates avoid immediate palette reuse for larger rosters");
  var day=WorldClock.Sample("w",3,.5f,4200,1200,1,true,false);check(day.NextPhase=="night"&&day.SecondsToTransition==420,"Sunset uses 85 percent of day length");
  var night=WorldClock.Sample("w",3,.9f,4680,1200,1,true,false);check(night.NextPhase=="day"&&night.SecondsToTransition==300,"Dawn wraps to 15 percent of next day");
  check(WorldClock.Sample("w",3,.1f,3660,1200,2,true,false).SecondsToTransition==60,"Time scaling changes real seconds");
  check(WorldClock.Sample("w",3,.5f,2400,4800,1,true,false).SecondsToTransition==1680,"Custom day length supported");
  check(!WorldClock.Sample("w",3,.5f,4200,1200,1,false,false).Running,"Empty-host pause stops countdown");
  check(WorldClock.Sample("w",3,.9f,4680,1200,1,true,true).SecondsToTransition==null,"Sleep skips do not claim normal-speed countdown");
  check(WorldClock.Sample("w",3,.5f,double.NaN,1200,1,true,false).SecondsToTransition==null,"Invalid host time rejected");
 }
}
