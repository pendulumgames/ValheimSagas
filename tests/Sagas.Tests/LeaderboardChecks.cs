using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ValheimSagas;

static class LeaderboardChecks {
 static JObject Json(object value)=>JObject.Parse(JsonConvert.SerializeObject(value,new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 public static async Task Run(string root,Action<bool,string> check){
  string path=Path.Combine(root,"leaderboard");var now=DateTime.UtcNow;var all=new TimeWindow(DateTime.MinValue,now);var day=new TimeWindow(now.AddDays(-1),now);
  SagaEvent Kill(string id,string player,string prefab,bool boss=true,double? duration=null)=>new(){Id=id,World="board",PlayerId=player,PlayerName=player=="hidden-id"?"Hidden name":"Untrusted report name",Kind="kill",Prefab=prefab,Name=prefab,Boss=boss,DurationSeconds=duration,Utc=now.AddHours(-1),Stars=1,X=12,Z=12};
  using(var service=new SagaService(new(){DataDirectory=path,ListenPrefix="",World="board"})){
   service.Store.Player(new(){World="board",PlayerId="a",Name="Astrid",ShareProfile=true,EpicLootInstalled=true,Gold=0});
   service.Store.Player(new(){World="board",PlayerId="b",Name="Bjorn",ShareProfile=true,Gold=30});
   service.Store.Player(new(){World="board",PlayerId="c",Name="Cora",ShareProfile=true});
   service.Store.Player(new(){World="board",PlayerId="hidden-id",Name="Hidden name",ShareProfile=false,Gold=99999,EpicLootInstalled=true});
   var first=Kill("e1","a","Eikthyr",duration:120);first.Contributors=new(){"a","b","b","hidden-id"};service.Store.AddEvent(first);
   var second=Kill("e2","b","Eikthyr",duration:90);second.Contributors=new(){"a"};service.Store.AddEvent(second);
   var elder=Kill("elder","hidden-id","gd_king",duration:30);elder.Contributors=new(){"b"};service.Store.AddEvent(elder);
   service.Store.AddEvent(Kill("private-fader","hidden-id","Fader",duration:1));
   var moder=Kill("moder","a","Dragon",duration:180);moder.Utc=now.AddDays(-2);moder.Contributors=new(){"b"};service.Store.AddEvent(moder);
   service.Store.AddEvent(Kill("legacy-bonemass","b","Bonemass"));
   service.Store.AddEvent(Kill("modded","c","ModdedBoss",duration:10));
   var normalA=Kill("normal-a","a","Troll",false);normalA.Stars=5;service.Store.AddEvent(normalA);
   var normalB=Kill("normal-b","b","Troll",false);normalB.Stars=5;service.Store.AddEvent(normalB);
   var foreign=Kill("foreign","a","Fader",duration:2);foreign.World="foreign";service.Store.AddEvent(foreign);
   foreach(var id in new[]{"a","b"}){
    service.Store.AddEvent(new(){Id="drop-"+id,World="board",PlayerId=id,Kind="drop",Name="Sword",Prefab="Sword",Rarity="Epic",RarityColor="#a335ee",Amount=10,Provenance="origin-"+id,Utc=now.AddHours(-1)});
    service.Store.AddEvent(new(){Id="collect-"+id,World="board",PlayerId=id,Kind="collect",Amount=10,Provenance="origin-"+id,Utc=now.AddHours(-1)});
   }
   service.Store.AddEvent(new(){Id="pickup-not-earned",World="board",PlayerId="a",Kind="pickup",Rarity="Legendary",Amount=500,Utc=now.AddHours(-1)});
   var bounty=new SagaEvent{Id="bounty-stable",World="board",PlayerId="a",Kind="bounty",Amount=1,Name="Bounty",Utc=now.AddHours(-1)};
   check(SagaService.ValidEvent(bounty)&&service.Store.AddEvent(bounty)&&!service.Store.AddEvent(bounty),"Real bounty completion kind validates and stable reports deduplicate");
   var invalid=Kill("invalid","a","Eikthyr",duration:double.NaN);check(!SagaService.ValidEvent(invalid),"Nonfinite boss timing rejected");invalid.DurationSeconds=0;check(!SagaService.ValidEvent(invalid),"Zero timing is unavailable, not fastest kill");invalid.DurationSeconds=604801;check(!SagaService.ValidEvent(invalid),"Unbounded timing rejected");
   bounty.Amount=2;check(!SagaService.ValidEvent(bounty),"A bounty event cannot inflate completion amount");
   check(!service.UpdatePlayer(new(){World="board",PlayerId="negative",Gold=-1}),"Negative carried Coins rejected");
   var result=Json(service.Leaderboard("board",all));
   var eikthyr=result["bosses"]!.Single(b=>(string?)b["key"]=="Eikthyr");
   check((int)eikthyr["uniquePlayers"]! ==2&&(int)eikthyr["kills"]! ==2,"Boss progression counts each consenting contributor and finisher once");
   check((double)eikthyr["fastest"]!["durationSeconds"]! ==90,"Per-boss fastest uses recorded duration");
   var elderRow=result["bosses"]!.Single(b=>(string?)b["key"]=="gd_king");
   check((int)elderRow["uniquePlayers"]! ==1&&elderRow["fastest"]!["finisher"]!.Type==JTokenType.Null&&(string?)elderRow["fastest"]!["team"]![0]!["name"]=="Bjorn","Private finisher still allows visible teammate credit without disclosing hidden person");
   check(!result.ToString().Contains("Hidden name")&&!result.ToString().Contains("hidden-id")&&!result.ToString().Contains("Untrusted report name"),"No private identities or untrusted historical player names leak");
   check((int)result["bosses"]!.Single(b=>(string?)b["key"]=="Fader")["kills"]! ==0,"Private-only and other-world victories are excluded");
   check(result["bosses"]!.Single(b=>(string?)b["key"]=="ModdedBoss")["order"]!.Type==JTokenType.Null,"Modded boss gets no fabricated progression tier");
   check((string?)result["progression"]![0]!["playerId"]=="b"&&(int)result["progression"]![0]!["highestOrder"]! ==4&&(int)result["progression"]![0]!["bossCount"]! ==4,"Furthest progress uses known tier then distinct credited bosses");
   check((string?)result["progression"]![0]!["bosses"]!.Single(b=>(string?)b["key"]=="Dragon")["firstTeam"]![0]!["name"]=="Astrid","Boss progression includes dated first team");
   check((int)result["kills"]![0]!["rank"]! ==1&&(int)result["kills"]![1]!["rank"]! ==1&&(string?)result["kills"]![0]!["name"]=="Astrid","Equal finishing-blow totals share rank with deterministic name order");
   check((int)result["highestStars"]![0]!["value"]! ==5&&(int)result["highestStars"]![1]!["rank"]! ==1,"Highest-star ties remain explicit");
   check((string?)result["highestStars"]![0]!["creatureName"]=="Troll"&&(string?)result["highestStars"]![0]!["prefab"]=="Troll"&&result["highestStars"]![0]!["utc"]!.Type==JTokenType.Date,"Highest-star ranking carries the actual creature and dated supporting kill");
   check(result["collectionsByRarity"]!.Count()==1&&(long)result["collectionsByRarity"]![0]!["rows"]![0]!["value"]! ==10,"Rarity rankings count earned collections, excluding drops and unknown pickups");
   check((int)result["availability"]!["untimedBossKills"]! ==1&&!result["fastestBossKills"]!.Any(b=>(string?)b["bossKey"]=="Bonemass"),"Untimed legacy kill retains progression but no invented speed");
   check(result["gold"]!.Count()==2&&(string?)result["gold"]![0]!["name"]=="Bjorn"&&(long)result["gold"]![1]!["value"]! ==0,"Gold includes known zero, omits unavailable/private, and does not require Epic Loot");
   check((int)result["bounties"]![0]!["value"]! ==1&&(bool)result["availability"]!["bountyTrackingAvailable"]!,"Bounty board counts durable completion events rather than pruned snapshot ledger");
   var narrowed=Json(service.Leaderboard("board",day));check((int)narrowed["progression"]![0]!["highestOrder"]! ==3,"Selected window applies to progression and speeds");
   var empty=Json(service.Leaderboard("board",new TimeWindow(now.AddMinutes(-1),now)));check(empty["bounties"]!.Count()==0&&empty["kills"]!.Count()==0&&empty["gold"]!.Count()==2,"Time filter clears event boards but preserves explicitly latest snapshot Gold");
   var selected=Json(service.Leaderboard("board",all,new(){"a"}));check(!selected.ToString().Contains("Bjorn")&&selected["progression"]!.Count()==1&&(int)selected["bosses"]!.Single(b=>(string?)b["key"]=="Eikthyr")["kills"]! ==2,"Player selection includes contributor participation without exposing others");
   var state=Json(service.State("board",all));check((int)state["stats"]!["bossKills"]! ==5&&(int)state["stats"]!["highestStars"]! ==5&&(long)state["stats"]!["rarityCollected"]!["epic"]! ==20,"Overview additions use complete visible finishing-blow and collection facts");
   check(!state["events"]!.ToString().Contains("hidden-id"),"Raw state event contributors redact private identities");
   service.Store.ApplyRetention(now,1,0);
   var retained=service.Store.Events("board",all).Single(e=>e.Id=="moder");
   check(retained.DetailsExpired&&retained.DurationSeconds==180&&retained.Contributors.Contains("b"),"Detail retention preserves statistical boss timing and team credit");
   check(Json(service.Leaderboard("board",all))["progression"]!.ToString()==result["progression"]!.ToString(),"Detail retention leaves full progression exact");
   service.Store.Player(new(){World="large-board",PlayerId="bulk",Name="Bulk",ShareProfile=true});
   for(int i=0;i<5001;i++)service.Store.AddEvent(new(){Id="bulk-"+i,World="large-board",PlayerId="bulk",Kind="kill",Prefab="Troll",Name="Troll",Stars=i==0?12:0,Utc=now.AddSeconds(-6000+i)});
   var capped=Json(service.State("large-board",all));var complete=Json(service.Leaderboard("large-board",all));
   check(capped["events"]!.Count()==5000&&(int)complete["kills"]![0]!["value"]! ==5001&&(int)complete["highestStars"]![0]!["value"]! ==12,"Leaderboard aggregates all 5001 events including evidence beyond recent-event cap");
  }
  using(var store=new SagaStore(path)){var retained=store.Events("board",all).Single(e=>e.Id=="moder");check(retained.DurationSeconds==180&&retained.Contributors.Contains("b")&&store.Events("board",all).Count(e=>e.Kind=="bounty")==1,"Timings, team and bounty facts survive restart");}
  int FreePort(){var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();return port;}
  var httpPort=FreePort();
  using(var service=new SagaService(new(){DataDirectory=path,World="board",ListenPrefix=$"http://127.0.0.1:{httpPort}/",RequireViewerToken=true,ViewerToken="leaderboard-test-token-at-least-24",LoreMilestoneEvents=999999})){
   service.Start();using var http=new HttpClient{BaseAddress=new Uri($"http://127.0.0.1:{httpPort}/")};
   check((await http.GetAsync("api/leaderboard?range=all")).StatusCode==HttpStatusCode.Unauthorized,"Leaderboard HTTP requires private token");
   http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer","leaderboard-test-token-at-least-24");
   var response=await http.GetAsync("api/leaderboard?range=all");var body=await response.Content.ReadAsStringAsync();
   check(response.IsSuccessStatusCode&&JObject.Parse(body)["bosses"]!.Count()==8&&!body.Contains("Hidden name"),"Authenticated HTTP leaderboard serializes complete privacy-filtered contract");
   check((await http.GetAsync("api/leaderboard?range=bogus")).StatusCode==HttpStatusCode.BadRequest,"Leaderboard invalid range returns HTTP400");
   check((await http.PostAsync("api/leaderboard",new StringContent("{}"))).StatusCode==HttpStatusCode.MethodNotAllowed,"Leaderboard endpoint is read-only");
  }
  httpPort=FreePort();
  using(var service=new SagaService(new(){DataDirectory=path,World="board",ListenPrefix=$"http://127.0.0.1:{httpPort}/",RequireViewerToken=false,LoreMilestoneEvents=999999})){
   service.Start();using var http=new HttpClient();var response=await http.GetAsync($"http://127.0.0.1:{httpPort}/api/leaderboard?range=all");
   check(response.IsSuccessStatusCode&&!(await response.Content.ReadAsStringAsync()).Contains("Hidden name"),"Explicit public access preserves leaderboard consent filtering");
  }
 }
}
