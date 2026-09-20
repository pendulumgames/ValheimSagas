using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ValheimSagas;

static class NemesisChecks {
 static JObject Json(object value)=>JObject.Parse(JsonConvert.SerializeObject(value,new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 public static void Run(string root,Action<bool,string> check){
  var path=Path.Combine(root,"nemesis");var now=DateTime.UtcNow;var all=new TimeWindow(DateTime.MinValue,now);
  SagaEvent Kill(string id,string finisher,bool flagged=true)=>new(){World="nemesis",Id=id,PlayerId=finisher,PlayerName=finisher,Kind="kill",Name="Nemesis Fader",Prefab="Fader",Boss=true,NemesisBoss=flagged,Utc=now.AddDays(-2),DurationSeconds=20,Contributors=new(){"a","a","private"}};
  using(var service=new SagaService(new(){World="nemesis",DataDirectory=path,ListenPrefix=""})){
   var store=service.Store;
   store.Player(new(){World="nemesis",PlayerId="a",Name="Astrid",NemesisScore=23.75f});
   store.Player(new(){World="nemesis",PlayerId="b",Name="Bjorn"});
   store.Player(new(){World="nemesis",PlayerId="private",Name="Hidden Viking",ShareProfile=false,NemesisScore=999});
   var kill=Kill("stable-death","b");check(store.AddEvent(kill)&&!store.AddEvent(kill),"Nemesis reports deduplicate by stable creature death identity");
   var hidden=Kill("hidden-only","private");hidden.Contributors.Clear();store.AddEvent(hidden);
   var recent=Kill("recent","a");recent.Utc=now.AddMinutes(-5);recent.Contributors.Clear();store.AddEvent(recent);
   var board=Json(service.Leaderboard("nemesis",all));
   check(board["nemesisBossKills"]!.Count()==2&&(long)board["nemesisBossKills"]![0]!["value"]! ==2&&(long)board["nemesisBossKills"]![1]!["value"]! ==1,"Nemesis leaderboard credits finisher and each unique consenting teammate once");
   check(!board.ToString().Contains("Hidden Viking")&&!board.ToString().Contains("\"private\""),"Nemesis leaderboard never discloses private team identities");
   check(board["progression"]!.Count()==0&&board["fastestBossKills"]!.Count()==0&&(int)board["bosses"]!.Single(b=>(string?)b["key"]=="Fader")["kills"]! ==0,"Nemesis vanilla prefab has no ordinary boss progression or fastest kill credit");
   check(store.CompletedBossCount("nemesis","a")==0&&store.CompletedBossCount("nemesis","b")==0,"Nemesis kills never create durable scenery receipts");
   var window=Json(service.Leaderboard("nemesis",new TimeWindow(now.AddHours(-1),now)));
   check(window["nemesisBossKills"]!.Count()==1&&(long)window["nemesisBossKills"]![0]!["value"]! ==1,"Nemesis rankings obey selected time window");
   var selected=Json(service.Leaderboard("nemesis",all,new(){"b"}));check(selected["nemesisBossKills"]!.Count()==1&&!selected.ToString().Contains("Astrid"),"Nemesis rankings honor selected player without leaking teammate");
   check(!(bool)board["availability"]!["nemesisTrackingAvailable"]!,"No SLS installation means optional Nemesis tracking availability is false");
   var context=string.Join("\n",store.NarrativeContext("nemesis","a").Facts);
   check(context.Contains("Nemesis boss defeats with contributor or finisher credit: 2")&&context.Contains("not evidence of vanilla boss progression"),"Lore reports actual Nemesis encounters and distinguishes vanilla progression");
   check(!context.Contains("current SLS Nemesis score")&&!context.Contains("Hidden Viking"),"Lore excludes stale score when optional SLS unavailable and private teammates");
   store.Sls("nemesis",new(){Installed=true,NemesisEnabled=true});
   check((bool)Json(service.Leaderboard("nemesis",all))["availability"]!["nemesisTrackingAvailable"]!,"Server installed Nemesis integration enables optional leaderboard availability");
   context=string.Join("\n",store.NarrativeContext("nemesis","a").Facts);
   check(context.Contains("current SLS Nemesis score 23.75")&&context.Contains("last reported value, not a lifetime total"),"Lore includes fractional current snapshot score with its factual limitations");
   check(!string.Join("\n",store.NarrativeContext("nemesis").Facts).Contains("999")&&store.NarrativeContext("nemesis","private").Facts.Count==0,"Private Nemesis scores cannot enter server or personal saga evidence");
   store.Sls("nemesis",new(){Installed=true,NemesisEnabled=false});
   check(!string.Join("\n",store.NarrativeContext("nemesis","a").Facts).Contains("current SLS Nemesis score"),"Disabled Nemesis system suppresses old score evidence");
   store.ApplyRetention(now,1,0);var compact=store.Events("nemesis",all).Single(e=>e.Id==kill.Id);
   check(compact.DetailsExpired&&compact.NemesisBoss&&compact.Contributors.Contains("a"),"Compact retained facts preserve Nemesis identity and contributor credit");
   check(Json(service.Leaderboard("nemesis",all))["nemesisBossKills"]!.ToString()==board["nemesisBossKills"]!.ToString(),"Detail retention preserves Nemesis standings exactly");
   var ordinary=Kill("ordinary-fader","a",false);ordinary.Contributors.Clear();store.AddEvent(ordinary);
   check(store.CompletedBossCount("nemesis","a")==1,"Actual ordinary Fader still unlocks normal progression");
   var noMod=Json(service.Leaderboard("vanilla",all));check(noMod["nemesisBossKills"]!.Count()==0&&!(bool)noMod["availability"]!["nemesisTrackingAvailable"]!&&noMod["bounties"]!.Count()==0,"Vanilla server works with both optional integrations absent");
  }
  using(var store=new SagaStore(path)){
   check(store.Events("nemesis",all).Single(e=>e.Id=="stable-death").NemesisBoss&&!store.AddEvent(Kill("stable-death","b")),"Nemesis classification and duplicate protection survive restart");
   check(store.Players("nemesis").Single(p=>p.PlayerId=="a").NemesisScore==23.75f,"Fractional Nemesis score persists without invented integer levels");
  }
 }
}
