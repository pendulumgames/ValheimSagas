using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json.Linq;
using ValheimSagas;

static class PlayerLoginChecks {
 public static async Task Run(string root,Action<bool,string> check){
  var path=Path.Combine(root,"player-login");var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
  const string shared="synthetic-shared-login-token-123456";var url="http://127.0.0.1:"+port+"/";string saved="";
  using(var service=new SagaService(new(){DataDirectory=path,ListenPrefix=url,RequireViewerToken=true,ViewerToken=shared,World="w"})){
   service.Start();foreach(var id in new[]{"owner","locked","teammate","progress"})service.Store.Player(new(){World="w",PlayerId=id,Name=id});
   var first=service.Store.IssuePlayerLogin("w","owner");saved=service.Store.IssuePlayerLogin("w","owner");var locked=service.Store.IssuePlayerLogin("w","locked");
   check(first!=saved&&saved.Length==70,"Personal tokens use independent 256-bit random credentials");
   check(service.Store.AuthenticatePlayer(first)?.PlayerId=="owner"&&service.Store.AuthenticatePlayer(saved)?.PlayerId=="owner","New login preserves existing browser authority");
   service.Store.Player(new(){World="w",PlayerId="sessions",Name="Synthetic sessions"});
   var credentials=new List<string>();for(int i=0;i<34;i++){credentials.Add(service.Store.IssuePlayerLogin("w","sessions"));System.Threading.Thread.Sleep(2);}
   check(credentials.Count(t=>service.Store.AuthenticatePlayer(t)!=null)==32,"Browser credential count is bounded");
   check(service.Store.AuthenticatePlayer(credentials.Last())?.PlayerId=="sessions","Most recently issued browser credential remains valid");
   service.Store.RevokePlayerLogin("w","sessions");check(credentials.All(t=>service.Store.AuthenticatePlayer(t)==null),"Revoke all covers every device credential");
   using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(10)};
   async Task<JObject> Request(string route,string token,int code=200,string? body=null){using var req=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,url+route);if(token!="")req.Headers.Authorization=new("Bearer",token);if(body!=null)req.Content=new StringContent(body,Encoding.UTF8,"application/json");using var response=await client.SendAsync(req);check((int)response.StatusCode==code,"Login HTTP "+route+" status "+code);return JObject.Parse(await response.Content.ReadAsStringAsync());}
   await Request("api/session", "",401);await Request("api/session",first,200);
   check((await Request("api/session",shared))["playerId"]!.Type==JTokenType.Null,"Shared token has no character authority");
   check((string?)(await Request("api/session",saved))["playerId"]=="owner","Personal session derives owner from bearer");
   var otherSession=await Request("api/session?world=other",saved);check((string?)otherSession["playerId"]=="owner"&&(string?)otherSession["world"]=="w","Personal identity remains bound to credential world while browsing another world");
   await Request("api/profile-background",shared,403,"{\"world\":\"w\",\"biome\":\"plains\"}");
   await Request("api/profile-background",locked,403,"{\"world\":\"w\",\"biome\":\"plains\"}");
   check(!new SagaOptions().RequireViewerToken,"New server options default to public viewing");
   var progress=service.Store.IssuePlayerLogin("w","progress");
   check((await Request("api/session",progress))["allowedBackgrounds"]!.Values<string>().SequenceEqual(new[]{"automatic","meadows"}),"New Viking may choose Meadows or automatic");
   await Request("api/profile-background",progress,200,"{\"world\":\"w\",\"biome\":\"meadows\"}");
   service.Store.AddEvent(new(){World="w",Id="progress-eikthyr",PlayerId="progress",Kind="kill",Boss=true,Prefab="Eikthyr",Utc=DateTime.UtcNow});
   check((await Request("api/session",progress))["allowedBackgrounds"]!.Values<string>().SequenceEqual(new[]{"automatic","meadows","black-forest"}),"Eikthyr unlocks through Black Forest");
   await Request("api/profile-background",progress,200,"{\"world\":\"w\",\"biome\":\"black-forest\"}");
   await Request("api/profile-background",progress,403,"{\"world\":\"w\",\"biome\":\"swamp\"}");
   service.Store.AddEvent(new(){World="w",Id="progress-bonemass",PlayerId="teammate",Contributors=new(){"progress"},Kind="kill",Boss=true,Prefab="Bonemass",Utc=DateTime.UtcNow});
   check((await Request("api/session",progress))["allowedBackgrounds"]!.Values<string>().SequenceEqual(new[]{"automatic","meadows","black-forest","swamp","mountain"}),"Bonemass team credit unlocks through Mountain despite missing Elder receipt");
   await Request("api/profile-background",progress,200,"{\"world\":\"w\",\"biome\":\"mountain\"}");
   await Request("api/profile-background",progress,403,"{\"world\":\"w\",\"biome\":\"plains\"}");
   var bosses=new[]{"Eikthyr","gd_king","Bonemass","Dragon","GoblinKing","SeekerQueen","Fader"};
   foreach(var boss in bosses)service.Store.AddEvent(new(){World="w",Id=boss,PlayerId="teammate",Kind="kill",Boss=true,Prefab=boss,Contributors=new(){"owner","owner"},Utc=DateTime.UtcNow});
   service.Store.AddEvent(new(){World="w",Id="only-fader",PlayerId="locked",Kind="kill",Boss=true,Prefab="Fader",Utc=DateTime.UtcNow});
   check((bool)(await Request("api/session",saved))["backgroundUnlocked"]!,"All seven team contributions unlock background");
   check((await Request("api/session",locked))["allowedBackgrounds"]!.Count()==9,"Fader participation unlocks every scenery through Deep North even with earlier bosses absent");
   await Request("api/profile-background",saved,400,"{\"world\":\"other\",\"biome\":\"plains\"}");
   await Request("api/profile-background",saved,400,"{\"world\":\"w\",\"biome\":\"https://evil\"}");
   await Request("api/profile-background",saved,400,"{\"world\":\"w\",\"biome\":\"plains\",\"playerId\":\"locked\"}");
   await Request("api/profile-background",saved,200,"{\"world\":\"w\",\"biome\":\"plains\"}");
   var state=await Request("api/state?range=30m",saved);var owner=state["players"]!.Single(p=>(string?)p["playerId"]=="owner");
   check((string?)owner["profileBiome"]=="Plains"&&(string?)owner["backgroundPreference"]=="plains","Saved choice projects into public state independently of filters");
   check(!state.ToString().Contains(saved)&&!state.ToString().Contains("hash"),"Public state contains no credential or hash");
   check(service.Store.BackgroundPreference("w","locked")=="automatic","Preference cannot affect another player");
   service.UpdatePlayer(new(){World="w",PlayerId="owner",Name="renamed",BackgroundUnlocked=true,BackgroundPreference="ashlands"});service.Flush();
   check((string?)(await Request("api/session",saved))["backgroundPreference"]=="plains","Incoming snapshot cannot overwrite server preference");
   await Request("api/profile-background",saved,200,"{\"world\":\"w\",\"biome\":\"automatic\"}");
   check((string?)(await Request("api/state",saved))["players"]!.Single(p=>(string?)p["playerId"]=="owner")["profileBiome"]=="DeepNorth","Automatic restores progression biome");
  }
  using(var database=new LiteDB.LiteDatabase(Path.Combine(path,"sagas.db"))){database.GetCollection("events").DeleteAll();database.GetCollection("statistics").DeleteAll();}
  using(var service=new SagaService(new(){DataDirectory=path,ListenPrefix=url,RequireViewerToken=false,World="w"})){
   check(service.Store.CompletedBossCount("w","owner")==7,"Durable boss receipts survive statistical retention and restart");
   var retained=JObject.FromObject(service.State("w",new TimeWindow(DateTime.MinValue,DateTime.MaxValue)))["players"]!.Single(p=>(string?)p["PlayerId"]=="owner");
   check((string?)retained["ProfileBiome"]=="DeepNorth"&&(string?)retained["ProfileBiomeEvidence"]=="Recorded team victory over Fader","Automatic biome and evidence survive complete event retention");
   check(service.Store.BackgroundPreference("w","progress")=="mountain"&&service.Store.CompletedBossCount("w","progress")==2,"Incremental scenery choice and durable participation persist across retention and restart");
   service.Start();check(service.Store.AuthenticatePlayer(saved)?.PlayerId=="owner","Personal token persists through server restart");
   using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(10)};using var anonymous=await client.GetAsync(url+"api/session");check(anonymous.StatusCode==HttpStatusCode.OK,"Public viewing needs no personal login");
   client.DefaultRequestHeaders.Authorization=new("Bearer","invalid");using var bad=await client.GetAsync(url+"api/session");check(bad.StatusCode==HttpStatusCode.Unauthorized,"Public mode rejects invalid supplied credential");
   var additional=service.Store.IssuePlayerLogin("w","owner");service.Store.RevokePlayerLogin("w","owner");check(service.Store.AuthenticatePlayer(additional)==null,"Revoke removes all browser credentials");check(service.Store.AuthenticatePlayer(saved)==null,"Explicit revocation removes login authority");
  }
  check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(path,"sagas.db"))).Contains(saved),"Database never persists plaintext personal credential");
 }
}
