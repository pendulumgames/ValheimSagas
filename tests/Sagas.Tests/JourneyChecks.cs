using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ValheimSagas;
static class JourneyChecks {
 static JObject Json(object x)=>JObject.Parse(JsonConvert.SerializeObject(x,new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 public static void Run(string root,Action<bool,string> check){
  using var service=new SagaService(new(){DataDirectory=Path.Combine(root,"journeys"),World="w"});var store=service.Store;var now=DateTime.UtcNow;
  foreach(var id in new[]{"a","b"}){store.Player(new(){World="w",PlayerId=id,Name=id,ShareProfile=true,ShareMap=true});store.Explore(new(){World="w",PlayerId=id,Cells=new(){new(){X=0,Z=0},new(){X=30,Z=0}}});}
  var ids=new List<string>();
  void Event(string id,int minutes,float x=10,string owner="a"){ids.Add(id);store.AddEvent(new(){Id=id,World="w",PlayerId=owner,Kind="kill",Name=id,Prefab="Eikthyr",Biome="Meadows",Boss=true,X=x,Z=10,Utc=now.AddMinutes(minutes)});}
  Event("one",-60);Event("two",-59,20);Event("hidden",-58,999);Event("after-hidden",-57,30);Event("b",-56,40,"b");Event("far",-55,1921);Event("late",0,1922);
  var c=new SagaChapter{Id="chapter",World="w",PlayerId="a",Title="A test saga",Text="Synthetic story",Model="fixture/story",Utc=now,EventIds=ids,Participants=new(){new(){PlayerId="a",Name="a"}}};store.Chapter(c);
  var data=Json(service.SagaJourney("w","chapter"));var m=data["moments"]!;
  check(m.Count()==6&&m.Select(x=>(string)x["id"]!).SequenceEqual(new[]{"one","two","after-hidden","b","far","late"}),"Journey chronological, discovered and world isolated");
  check(!(bool)m[1]!["breakBefore"]!&&m.Where((_,i)=>i!=1).All(x=>(bool)x["breakBefore"]!),"Trail breaks on hidden events, different players, distance and time gaps");
  check((string)m[0]!["prefab"]! =="Eikthyr"&&(string)m[0]!["biome"]! =="Meadows","Journey uses recorded prefab and biome for local artwork");
  check(Json(service.SagaJourney("other","chapter"))["chapter"]!.Type==JTokenType.Null,"No chapter across world boundary");
  check(Json(service.SagaJourney("w","chapter",new(){"b"}))["chapter"]!.Type==JTokenType.Null,"Selected map owner cannot expose another Viking chapter");
  var feed=Json(service.SagaJourneys("w"));check(feed["chapters"]!.Count()==1&&feed["chapters"]![0]!["anchor"]!["id"]!.Value<string>()=="one","Independent index uses shared explored anchor");
  check(feed["chapters"]![0]!["chapter"]!["text"]!.Value<string>()=="","Index omits full chapter text");
  store.Player(new(){World="w",PlayerId="a",Name="a",ShareProfile=true,ShareMap=false});data=Json(service.SagaJourney("w","chapter"));check(data["moments"]!.Count()==1&&(string)data["moments"]![0]!["playerId"]! =="b","Map withdrawal immediately removes affected coordinates");
  store.Player(new(){World="w",PlayerId="a",Name="a",ShareProfile=false,ShareMap=true});check(!Json(service.SagaJourneys("w"))["chapters"]!.Any()&&Json(service.SagaJourney("w","chapter"))["chapter"]!.Type==JTokenType.Null,"Profile withdrawal hides index and detail");
  store.Player(new(){World="w",PlayerId="a",Name="a",ShareProfile=true,ShareMap=true});c.MapParticipants.Add("b");store.Chapter(c);store.Player(new(){World="w",PlayerId="b",Name="b",ShareProfile=true,ShareMap=false});check(Json(service.SagaJourney("w","chapter"))["chapter"]!.Type==JTokenType.Null,"Prose with withdrawn map context is hidden too");
  c.MapParticipants.Clear();store.Chapter(c);
  for(int i=0;i<30;i++)store.Chapter(new(){Id="page"+i,World="w",PlayerId="a",Utc=now.AddMinutes(i+1),Model="fixture/story",Text="Test",Title="Page"+i,EventIds=new(){"one"}});
  feed=Json(service.SagaJourneys("w"));var second=Json(service.SagaJourneys("w",null,(string)feed["next"]!));check(feed["chapters"]!.Count()==24&&second["chapters"]!.Count()==7,"Chapter index bounded with older-page cursor");
  check(!feed["chapters"]!.Select(x=>(string)x["chapter"]!["id"]!).Intersect(second["chapters"]!.Select(x=>(string)x["chapter"]!["id"]!)).Any(),"Paging does not duplicate chapters");
  check(!Json(service.SagaJourneys("w",null,"unknown"))["chapters"]!.Any(),"Invalid cursor cannot repeat unbounded first page");
  var crowded=new List<string>();
  for(int i=0;i<20;i++){var id="crowded"+i;crowded.Add(id);if(i!=10)store.AddEvent(new(){Id=id+"-drop",Provenance=id,World="w",PlayerId="a",Kind="drop",Prefab=i==15?"Sword":"Resin",Amount=1,Rarity=i==15?"Legendary":"",Utc=now.AddSeconds(i)});store.AddEvent(new(){Id=id,Provenance=i==10?"":id,World="w",PlayerId="a",Kind=i==10?"kill":"collect",Prefab=i==10?"Eikthyr":i==15?"Sword":"Resin",Name=id,Boss=i==10,Rarity=i==15?"Legendary":"",Biome=i<10?"Meadows":"BlackForest",X=10+i,Z=10,Utc=now.AddSeconds(i)});}
  store.Chapter(new(){Id="crowded",World="w",PlayerId="a",Model="fixture/story",Text="Test",EventIds=crowded});
  var highlights=Json(service.SagaJourney("w","crowded"))["moments"]!.ToArray();
  check(highlights.Length==6,"Dense chapter limited to six meaningful highlights");
  check(highlights.Any(m=>(string)m["id"]! =="crowded10")&&highlights.Any(m=>(string)m["id"]! =="crowded15"),"Boss and rare discovery survive repetitive ordinary loot");
  check(highlights.Select(m=>(DateTime)m["utc"]!).SequenceEqual(highlights.Select(m=>(DateTime)m["utc"]!).OrderBy(t=>t)),"Highlights retain chronology");
  check(highlights.Skip(1).All(m=>!(bool)m["breakBefore"]!),"Omitted ordinary evidence does not break a continuous known journey");
  var sceneChapter=new SagaChapter{Id="scenes",World="w",PlayerId="a",Model="fixture/story",Text="Original full story",EventIds=ids,Scenes=new(){new(){Title="The approach",Text="A story passage before the storm.",EventIds=new(){"one","two"}},new(){Title="Hidden crossing",Text="This passage must stay hidden.",EventIds=new(){"hidden","after-hidden"}},new(){Title="The far shore",Text="A story passage after the crossing.",EventIds=new(){"late"}}}};store.Chapter(sceneChapter);
  var sceneResult=Json(service.SagaJourney("w","scenes"));var sceneMoments=sceneResult["moments"]!.ToArray();
  check(sceneMoments.Length==2&&(string)sceneMoments[0]["sceneTitle"]! =="The approach","Journey uses authored scenes rather than unrelated ranked records");
  check(!sceneResult["moments"]!.ToString().Contains("Hidden crossing")&&(bool)sceneMoments[1]["breakBefore"]!,"Unavailable scene evidence hides its passage and breaks the next connection");
  check(sceneMoments[0]["evidenceIds"]!.Count()==2,"Scene preserves grouped evidence behind its anchor");
  check(store.Chapters("w").Single(x=>x.Id=="chapter").Scenes.Count==0,"Existing chapter is never automatically converted");
  c.Model="local-template";store.Chapter(c);check(Json(service.SagaJourney("w","chapter"))["chapter"]!.Type==JTokenType.Null,"No local template saga substituted");
 }
}
