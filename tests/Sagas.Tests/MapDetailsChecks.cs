using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ValheimSagas;
static class MapDetailsChecks {
 static JObject Json(object value)=>JObject.Parse(JsonConvert.SerializeObject(value,new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 public static void Run(string root,Action<bool,string> check){
  var options=new SagaOptions{DataDirectory=Path.Combine(root,"map-details"),World="pins",ListenPrefix=""};
  using(var s=new SagaService(options)){
   s.Start();var p=new PlayerSnapshot{World="pins",PlayerId="a",Name="Astrid",ShareMap=true,ShareProfile=false};
   s.UpdatePlayer(p);s.UpdatePlayer(new(){World="pins",PlayerId="b",ShareMap=true});
   var rgba=new byte[TerrainTile.RenderedByteCount];rgba[(64+1)*4+3]=255;
   s.Explore(new(){World="pins",PlayerId="a",Cells=new(){new(){X=0,Z=0},new(){X=1,Z=0,TerrainPixels=Convert.ToBase64String(rgba)}}});
   s.Explore(new(){World="pins",PlayerId="b",Cells=new(){new(){X=2,Z=0}}});s.Flush();
   MapPins Batch(string revision,int index=0,int count=1)=>new(){World="pins",PlayerId="a",Revision=revision,Index=index,Count=count};
   MapPin Pin(float x,bool personal=false)=>new(){Name="Synthetic pin",Type="Boss",X=x,Z=1,Personal=personal};
   JArray Pins(HashSet<string>? ids=null)=>(JArray)Json(s.MapIcons("pins",ids))["pins"]!;
   var first=Batch("one");first.Pins=new(){Pin(1),Pin(2,true),Pin(65),Pin(95),Pin(129)};
   check(s.UpdatePins(first),"Discovered-pin snapshot accepted");first.Pins.Clear();s.Flush();
   check(Pins().Count==2,"Personal pins default off, transparent fog and other-owner-only exploration excluded; profile permission independent");
   check(Pins(new(){"b"}).Count==0,"Selected explorers restrict pins");
   check(((JArray)Json(s.MapIcons("elsewhere"))["pins"]!).Count==0,"Pins isolated by world");
   p.SharePins=true;s.UpdatePlayer(p);s.Flush();check(Pins().Count==3,"Personal pins require explicit owner consent");
   var a=Batch("two",0,2);a.Pins.Add(Pin(3));var b=Batch("two",1,2);b.Pins.Add(Pin(4));int acks=0;
   s.UpdatePins(b,ok=>{if(ok)acks++;});s.Flush();check(Pins().Count==3&&acks==0,"Incomplete out-of-order snapshot neither replaces pins nor acknowledges");
   s.UpdatePins(a,ok=>{if(ok)acks++;});s.Flush();check(Pins().Count==2&&acks==2,"Complete multipart snapshot atomically replaces deleted pins and acknowledges all parts");
   s.UpdatePins(a,ok=>{if(ok)acks++;});s.Flush();check(acks==3&&Pins().Count==2,"Lost ACK retry acknowledged without requiring other parts or duplicating pins");
   p.ShareMap=false;s.UpdatePlayer(p);s.Flush();check(Pins().Count==0,"Revoking map sharing hides persisted game and personal pins");p.ShareMap=true;s.UpdatePlayer(p);s.Flush();
   var invalid=Batch("bad");invalid.Pins.Add(Pin(float.NaN));check(!s.UpdatePins(invalid),"Nonfinite pin coordinates rejected");invalid.Pins=new(){Pin(20001)};check(!s.UpdatePins(invalid),"Out-of-world pin rejected");invalid.Pins=new(){Pin(1)};invalid.Count=65;check(!s.UpdatePins(invalid),"Snapshot size bounded");invalid.Count=1;invalid.Index=1;check(!s.UpdatePins(invalid),"Invalid snapshot index rejected");invalid.Index=0;invalid.Pins[0].Name=new string('x',101);check(!s.UpdatePins(invalid),"Pin text bounded");
   var duplicate=Batch("b");duplicate.PlayerId="b";duplicate.Pins=new(){Pin(3)};s.Explore(new(){World="pins",PlayerId="b",Cells=new(){new(){X=0,Z=0}}});s.UpdatePins(duplicate);s.Flush();check(Pins().Count==2,"Same game location from multiple explorers deduplicated");
   s.UpdateClock(new(){World="pins",Day=12,Fraction=.5f});check(s.Clock("pins")?.Day==12&&s.Clock("pins")?.Fraction==.5f,"Host clock retained");s.UpdateClock(new(){World="pins",Fraction=float.NaN});check(s.Clock("pins")?.Day==12,"Invalid clock rejected");check(s.Clock("other")==null,"Clock isolated by world");
  }
  using(var s=new SagaService(options)){s.Start();check(((JArray)Json(s.MapIcons("pins"))["pins"]!).Count==2,"Pins persist while owners offline after restart");check(s.Clock("pins")==null,"Restart does not pretend stale clock is live");s.UpdatePins(new(){World="pins",PlayerId="a",Revision="empty"});s.Flush();check(((JArray)Json(s.MapIcons("pins",new(){"a"}))["pins"]!).Count==0,"Empty snapshot removes last pin");}
 }
}
