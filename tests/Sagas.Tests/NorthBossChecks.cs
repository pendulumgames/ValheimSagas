using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ValheimSagas;
static class NorthBossChecks {
 static JObject Json(object x)=>JObject.Parse(JsonConvert.SerializeObject(x,new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 public static void Run(string root,Action<bool,string> check){
  var path=Path.Combine(root,"north-boss");var now=DateTime.UtcNow;var all=new TimeWindow(DateTime.MinValue,DateTime.MaxValue);
  using(var s=new SagaService(new(){DataDirectory=path,ListenPrefix="",World="north"})){
   foreach(var id in new[]{"champion","team","private","phase-only"})s.Store.Player(new(){World="north",PlayerId=id,Name=id,ShareProfile=id!="private",CompletedBossKeys=new(){"Eikthyr","Fader"},ProgressionTier=999,ProgressionBoss="Spoofed",LastAchievementUtc=now.AddYears(1)});
   foreach(var prefab in new[]{"FrozenKing","FrozenKing_p2","FrozenKing_p3(Clone)"})s.Store.AddEvent(new(){Id=prefab,World="north",PlayerId=prefab.Contains("p3")?"champion":"phase-only",Kind="kill",Boss=true,Prefab=prefab,DurationSeconds=90,Utc=now,Contributors=prefab.Contains("p3")?new(){"team","team","private"}:new()});
   var board=Json(s.Leaderboard("north",all));var boss=board["bosses"]!.Single(b=>(string?)b["key"]=="FrozenKing_p3");
   check((int)boss["kills"]! ==1&&(int)boss["uniquePlayers"]! ==2,"North final phase counts once with distinct consenting team");
   check((string?)boss["name"]=="Kall Fimbulbringer"&&(int)boss["order"]! ==8,"Installed north identity is final vanilla tier");
   check(!board["bosses"]!.Any(b=>(string?)b["key"] is "FrozenKing" or "FrozenKing_p2"),"Earlier phases do not become extra boss leaderboard rows");
   check(board["fastestBossKills"]!.Count()==0,"Phase-only duration is not advertised as full fight time");
   var data=Json(s.State("north",new TimeWindow(now.AddDays(-4),now.AddDays(-3)),new(){"phase-only"}));
   var champion=data["players"]!.Single(p=>(string?)p["playerId"]=="champion");var privacy=data["players"]!.Single(p=>(string?)p["playerId"]=="private");
   check((int)champion["progressionTier"]! ==8&&(string?)champion["profileBiome"]=="DeepNorth","North victory safely retains final biome and ignores analytics filter");
   check(champion["completedBossKeys"]!.Select(x=>(string)x!).SequenceEqual(new[]{"FrozenKing_p3"}),"Progress shows actual distinct boss receipts, not inferred earlier tiers or spoofed keys");
   check(privacy["completedBossKeys"]!.Count()==0,"Private boss tracker clears incoming client keys");
   check(data["bossCatalog"]!.Count()==8&&(string?)data["bossCatalog"]!.Last()!["key"]=="FrozenKing_p3","State supplies canonical eight-boss tracker labels");
   check((DateTime)champion["lastAchievementUtc"]! ==now,"Recent achievement derived from recorded victory, not incoming client fields");
   check((int)privacy["progressionTier"]! ==0&&(string?)privacy["progressionBoss"]==""&&privacy["lastAchievementUtc"]!.Type==JTokenType.Null,"Private achievements never populate directory metadata");
   check(s.Store.CompletedBossCount("north","phase-only")==0&&s.Store.CompletedBossCount("north","team")==1,"Only final phase grants persistent participation receipt");
   s.Store.ApplyRetention(now.AddDays(20),1,1);
  }
  using(var s=new SagaService(new(){DataDirectory=path,ListenPrefix="",World="north"})){
   var p=Json(s.State("north",all))["players"]!.Single(p=>(string?)p["playerId"]=="champion");
   check((int)p["progressionTier"]! ==8&&p["lastAchievementUtc"]!.Type==JTokenType.Null,"Progression persists after retention/restart without inventing a recent achievement date");
  }
  var legacy=Path.Combine(root,"north-legacy");
  using(var store=new SagaStore(legacy)){
   store.Player(new(){World="legacy",PlayerId="p",Name="Legacy"});
   store.AddEvent(new(){Id="old-final",World="legacy",PlayerId="p",Kind="kill",Boss=true,Prefab="FrozenKing_p3",Utc=now});
  }
  // Isolated pre-upgrade fixture: retained final death, old seven-boss receipts.
  using(var db=new LiteDB.LiteDatabase(Path.Combine(legacy,"sagas.db"))){db.GetCollection("bossReceipts").DeleteAll();db.GetCollection("meta").Delete("bossReceiptsMigrationV2");}
  using(var store=new SagaStore(legacy))check(store.CompletedBossKeys("legacy","p").Contains("FrozenKing_p3"),"Upgrade backfills north receipt from existing retained final death");

 }
}
