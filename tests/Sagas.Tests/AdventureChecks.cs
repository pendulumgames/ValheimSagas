using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ValheimSagas;
static class AdventureChecks {
 static JObject Json(object o)=>JObject.Parse(JsonConvert.SerializeObject(o,new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 public static void Run(string root,Action<bool,string> check){
  using var service=new SagaService(new(){DataDirectory=Path.Combine(root,"adventures"),World="w"});var s=service.Store;var now=DateTime.UtcNow.AddMinutes(-1);var all=new TimeWindow(DateTime.MinValue,DateTime.MaxValue);
  foreach(var id in new[]{"a","b","private"})s.Player(new(){World="w",PlayerId=id,Name=id,ShareProfile=id!="private",ShareMap=true});
  s.Explore(new(){World="w",PlayerId="a",Cells=new(){new(){X=0,Z=0}}});
  SagaEvent Event(string id,string kind="kill",string player="a")=>new(){Id=id,World="w",PlayerId=player,Kind=kind,Name=id,Prefab="Eikthyr",Utc=now,Amount=3,X=10,Z=10};
  var first=Event("first");first.Boss=true;first.DurationSeconds=60;first.Contributors=new(){"b","b","private"};first.Utc=now.AddHours(-2);s.AddEvent(first);
  var fast=Event("fast");fast.Boss=true;fast.DurationSeconds=30;s.AddEvent(fast);
  var north=Event("north");north.Boss=true;north.Prefab="FrozenKing_p3";north.DurationSeconds=5;s.AddEvent(north);
  var phase=Event("phase");phase.Boss=true;phase.Prefab="FrozenKing_p2";s.AddEvent(phase);
  var nemesis=Event("nemesis");nemesis.Boss=true;nemesis.NemesisBoss=true;s.AddEvent(nemesis);
  var loot=Event("loot","collect");loot.Rarity="Epic";loot.Provenance="loot";var lootDrop=Event("lootDrop","drop");lootDrop.Rarity="Epic";lootDrop.Provenance="loot";s.AddEvent(lootDrop);s.AddEvent(loot);s.AddEvent(Event("drop","drop"));s.AddEvent(Event("unknown","pickup"));s.AddEvent(Event("death","death"));s.AddEvent(Event("hidden","kill","private"));
  var elsewhere=Event("otherworld");elsewhere.World="other";s.AddEvent(elsewhere);
  var far=Event("far","collect");far.Rarity="Rare";far.X=999;far.Provenance="far";var farDrop=Event("farDrop","drop");farDrop.Rarity="Rare";farDrop.Provenance="far";s.AddEvent(farDrop);s.AddEvent(far);
  var chapter=new SagaChapter{Id="chapter",World="w",PlayerId="a",Utc=now,Model="test-fiction",Text="Test chapter",EventIds=new(){"first","far","hidden","otherworld"},Participants=new(){new(){PlayerId="a"}}};s.Chapter(chapter);
  var data=Json(service.Adventures("w",all,now.AddDays(-1)));var trophies=data["trophies"]!;var eikthyr=trophies.Single(t=>(string?)t["key"]=="Eikthyr");
  check((int)eikthyr["kills"]! ==2,"Ordinary trophies exclude Nemesis and phase deaths");check((double)eikthyr["first"]!["durationSeconds"]! ==60&&(double)eikthyr["fastest"]!["durationSeconds"]! ==30,"First and fastest victories have separate evidence");
  check(eikthyr["first"]!["team"]!.Count()==2&&!data.ToString().Contains("private"),"Teams deduplicate contributors and hide private profiles");
  check(trophies.Single(t=>(string?)t["key"]=="FrozenKing_p3")["fastest"]!.Type==JTokenType.Null,"North incomplete phase timings never enter fastest fights");
  var a=data["comparisons"]!.Single(p=>(string?)p["playerId"]=="a");var b=data["comparisons"]!.Single(p=>(string?)p["playerId"]=="b");
  check((int)a["collected"]! ==6&&(int)a["drops"]! ==9,"Collected versus dropped and unknown pickups stay distinct");check((int)b["bossParticipations"]! ==1&&(int)b["kills"]! ==0,"Contributor boss credit does not invent finishing kills");
  check(data["recap"]!["moments"]!.Single(e=>(string?)e["id"]=="far")["x"]!.Type==JTokenType.Null,"Undiscovered recap coordinates are redacted");
  check(data["recap"]!["moments"]!.Single(e=>(string?)e["id"]=="loot")["x"]!.Type!=JTokenType.Null,"Shared discovered event may link to atlas");
  var mapped=Json(service.ChapterMap("w","chapter"));check(mapped["moments"]!.Count()==1&&(string?)mapped["moments"]![0]!["id"]=="first","Saga map uses only recorded shared discovered chapter events");
  check(!Json(service.ChapterMap("other","chapter"))["moments"]!.Any(),"Chapter locations isolated by world");check(!Json(service.ChapterMap("w","absent"))["moments"]!.Any(),"Missing chapter yields no locations");
  var period=Json(service.Adventures("w",new TimeWindow(now.AddMinutes(-1),now.AddMinutes(1)),now.AddDays(-1)));check((int)period["trophies"]!.Single(t=>(string?)t["key"]=="Eikthyr")["kills"]! ==1,"Trophies honor period without changing recap");
  var selected=Json(service.Adventures("w",all,now.AddDays(-1),new(){"b"}));check(selected["comparisons"]!.Count()==1&&(int)selected["trophies"]!.Single(t=>(string?)t["key"]=="Eikthyr")["kills"]! ==1,"Selected contributor still receives recorded boss participation");
  s.Player(new(){World="w",PlayerId="a",Name="a",ShareProfile=true,ShareMap=false});check(!Json(service.ChapterMap("w","chapter"))["moments"]!.Any(),"Map sharing withdrawal immediately removes chapter coordinates");
  s.Player(new(){World="w",PlayerId="a",Name="a",ShareProfile=false,ShareMap=true});check(!Json(service.ChapterMap("w","chapter"))["moments"]!.Any(),"Profile withdrawal immediately hides chapter and locations");
  s.Player(new(){World="w",PlayerId="a",Name="a",ShareProfile=true,ShareMap=true});chapter.Participants.Add(new(){PlayerId="private"});s.Chapter(chapter);check(!Json(service.ChapterMap("w","chapter"))["moments"]!.Any(),"Chapter participant consent enforced before map links");
  for(int i=0;i<110;i++)s.AddEvent(Event("cap"+i,"death"));var capped=Json(service.Adventures("w",all,now.AddDays(-1)));check(capped["recap"]!["moments"]!.Count()==100&&(bool)capped["recap"]!["truncated"]!,"Recap bounded independently of exact totals");
  check(!Json(service.Adventures("w",all,DateTime.UtcNow.AddDays(1)))["recap"]!["moments"]!.Any(),"Future visitor timestamp clamped safely");
 }
}

