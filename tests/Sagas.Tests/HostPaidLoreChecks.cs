using System.Collections.Concurrent;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValheimSagas;

static class HostPaidLoreChecks {
 public static async Task Run(string root,Action<bool,string> check){
  const string world="synthetic-paid-host";
  var directory=Path.Combine(root,"host-paid-preset");
  using(var store=new SagaStore(directory))foreach(var id in new[]{"a","b"}){
   store.Player(new(){World=world,PlayerId=id,Name="Synthetic "+id,ShareProfile=true});
   store.AddEvent(new(){Id="paid-host-"+id,World=world,PlayerId=id,PlayerName="Synthetic "+id,Kind="kill",Name="Troll",Stars=3,Utc=DateTime.UtcNow.AddMinutes(-5)});
   store.QueueLore(world,id);
  }
  var handler=new Handler();
  var options=new SagaOptions{DataDirectory=directory,ListenPrefix="",World=world,LoreEnabled=true,OpenRouterKey="synthetic-host-key",LoreAllowPaid=true,LoreUseAccountPricing=true,LoreModel="@preset/shared-sagas",LoreDailyBudget=3,LoreMilestoneEvents=1};
  using(var service=new SagaService(options,new HttpClient(handler))){
   service.Start();var until=DateTime.UtcNow.AddSeconds(25);
   while(service.Store.Chapters(world).Count<2||service.Store.ServerChapters(world).Count<1){if(DateTime.UtcNow>until)throw new Exception("Paid host synthetic worker timed out");await Task.Delay(40);}
   check(handler.Bodies.Count==3&&handler.Keys.All(x=>x=="synthetic-host-key"),"One host key funds two default character sagas and the shared server saga");
   check(handler.Bodies.All(x=>(string?)x["model"]=="@preset/shared-sagas"&&x.Property("provider")==null),"Actual host worker inherits paid preset without replacing preset pricing/provider controls");
   check(!service.Store.ReserveLoreRequest(DateTime.UtcNow,3),"Personal default and server sagas share the host daily attempt budget");
   check(service.Store.Chapters(world).All(x=>x.Model=="synthetic/paid-model")&&service.Store.ServerChapters(world).Single().Model=="synthetic/paid-model","Host-paid actual model provenance persists for both scopes");
   check(!JsonConvert.SerializeObject(service.State(world,new TimeWindow(DateTime.MinValue,DateTime.UtcNow))).Contains("synthetic-host-key"),"Host credential is absent from viewer state");
  }
  using var reopened=new SagaStore(directory);
  check(reopened.Chapters(world).Count==2&&reopened.ServerChapters(world).Count==1,"Host-paid chapters survive restart");
 }
 sealed class Handler:HttpMessageHandler {
  internal readonly ConcurrentQueue<JObject> Bodies=new();internal readonly ConcurrentQueue<string> Keys=new();
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
   Bodies.Enqueue(JObject.Parse(await request.Content!.ReadAsStringAsync(token)));Keys.Enqueue(request.Headers.Authorization?.Parameter??"");
   return new(HttpStatusCode.OK){Content=new StringContent(JsonConvert.SerializeObject(new{model="synthetic/paid-model",choices=new[]{new{message=new{content=JsonConvert.SerializeObject(new{title="Synthetic host-funded chapter",text="Synthetic paid host narrative for automated routing and persistence verification; no external generation occurred.",characterBio="Synthetic character biography from the host-funded test preset.",serverBio="Synthetic fellowship biography from the host-funded test preset."})}}}}))};
  }
 }
}
