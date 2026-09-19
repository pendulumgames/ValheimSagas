using ValheimSagas;
using Newtonsoft.Json.Linq;
internal static class FilterIntegrationChecks {
 public static void Run(string root,Action<bool,string> check){
  var directory=Path.Combine(root,"filter-integration");var now=DateTime.UtcNow.AddDays(-1);var ranges=new[]{"30m","1h","6h","12h","1d","3d","7d"};
  using(var store=new SagaStore(directory))foreach(var range in ranges){var window=TimeWindow.Parse(range,null,null,now);store.Player(new(){World=range,PlayerId="a",Name="A"});store.Player(new(){World=range,PlayerId="b",Name="B"});var times=new[]{window.From.AddTicks(-1),window.From,window.To,window.To.AddTicks(1)};for(int i=0;i<4;i++)store.AddEvent(new(){Id="death-"+i,World=range,PlayerId="a",Kind="kill",Utc=times[i],Stars=i==2?5:0,Name="Troll",Prefab="Troll",Biome="BlackForest",Boss=i==2});store.AddEvent(new(){Id="other-player",World=range,PlayerId="b",Kind="kill",Utc=window.From,Stars=1,Name="Boar",Prefab="Boar"});}
  using(var service=new SagaService(new(){DataDirectory=directory,ListenPrefix=""}))foreach(var range in ranges){var window=TimeWindow.Parse(range,null,null,now);var state=JObject.FromObject(service.State(range,window,new(){"a"}));check((int)state["stats"]!["kills"]! ==2,range+" exact persisted boundary/character totals");check((int)state["stats"]!["stars"]!["0"]! ==1&&(int)state["stats"]!["stars"]!["5"]! ==1,range+" persisted high-star breakdown");check(service.Store.Events(range,window).Count==3&&service.Store.Events(range,new(DateTime.MinValue,DateTime.MaxValue)).Count==5,range+" everyone and all history after restart");}
 }
}
