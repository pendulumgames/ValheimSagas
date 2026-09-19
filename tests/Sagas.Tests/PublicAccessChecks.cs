using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using ValheimSagas;
static class PublicAccessChecks {
 static string Prefix(){var probe=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();return $"http://127.0.0.1:{port}/";}
 public static async Task Run(string root,Action<bool,string> check){
  var privateOptions=new SagaOptions{DataDirectory=Path.Combine(root,"private-access"),ListenPrefix=Prefix(),RequireViewerToken=true,ViewerToken="synthetic-private-access-token",World="private-world"};
  using(var service=new SagaService(privateOptions)){
   service.Start();using var client=new HttpClient{BaseAddress=new Uri(privateOptions.ListenPrefix)};
   var access=JObject.Parse(await client.GetStringAsync("api/access"));check(access.Properties().Count()==1&&access["requiresToken"]!.Type==JTokenType.Boolean&&(bool)access["requiresToken"]!,"Unauthenticated access discovery returns only private-access boolean");
   foreach(var endpoint in new[]{"api/state","api/map","api/media/"+new string('a',64)+"?player=p"})check((await client.GetAsync(endpoint)).StatusCode==HttpStatusCode.Unauthorized,"Private endpoint requires authentication: "+endpoint.Split('?')[0]);
   client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer","wrong-token");check((await client.GetAsync("api/state")).StatusCode==HttpStatusCode.Unauthorized,"Incorrect private bearer token rejected");
  }
  using(var invalid=new SagaService(new(){DataDirectory=Path.Combine(root,"invalid-key"),ListenPrefix=Prefix(),RequireViewerToken=true,ViewerToken="short"})){
   bool rejected=false;try{invalid.Start();}catch(ArgumentException){rejected=true;}check(rejected,"Private listener startup rejects invalid viewer credential");
  }
  var options=new SagaOptions{DataDirectory=Path.Combine(root,"public-access"),ListenPrefix=Prefix(),World="public-world",ViewerToken="",LoreMilestoneEvents=99999};
  using(var service=new SagaService(options)){
   service.Start();using var client=new HttpClient{BaseAddress=new Uri(options.ListenPrefix)};
   var access=JObject.Parse(await client.GetStringAsync("api/access"));check(access.Properties().Count()==1&&!(bool)access["requiresToken"]!,"Public listener starts without credentials and advertises access boolean only");
   var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=");var id=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
   PlayerSnapshot Player(params int[] slots)=>new(){World=options.World,PlayerId="p",Name="Synthetic Hotbar",ShareProfile=true,PortraitStatus="Waiting for synthetic portrait",Hotbar=slots.Select(slot=>new GearItem{HotbarSlot=slot,Name="Synthetic item "+slot,IconId=id,Equipped=slot==1,Active=slot==1}).ToList()};
   check(service.UpdatePlayer(Player(1,2,3,4,5,6,7,8)),"Eight unique numbered hotbar slots accepted");
   check(!service.UpdatePlayer(Player(1,1)),"Duplicate hotbar slots rejected");check(!service.UpdatePlayer(Player(0)),"Zero hotbar slot rejected");check(!service.UpdatePlayer(Player(9)),"Out-of-range hotbar slot rejected");check(!service.UpdatePlayer(Player(1,2,3,4,5,6,7,8,9)),"Ninth hotbar item rejected");
   service.UploadMedia(new(){World=options.World,PlayerId="p",Id=id,Kind="icon",Png=png});check(service.Flush(),"Public hotbar and artwork stored");
   foreach(var endpoint in new[]{"api/state","api/map","api/health"})check((await client.GetAsync(endpoint)).IsSuccessStatusCode,"Configured public read endpoint accessible: "+endpoint);
   var response=await client.GetAsync($"api/media/{id}?player=p");check(response.IsSuccessStatusCode&&(await response.Content.ReadAsByteArrayAsync()).SequenceEqual(png),"Public artwork permits referenced hotbar icon without gear or portrait reference");
   foreach(var endpoint in new[]{"api/access","api/state","api/map","api/health","api/media/"+id})check((await client.PostAsync(endpoint,new StringContent("{}"))).StatusCode==HttpStatusCode.MethodNotAllowed,"Public API remains read-only: "+endpoint);
   var privatePlayer=Player(1,2);privatePlayer.ShareProfile=false;service.UpdatePlayer(privatePlayer);service.Flush();
   var state=JObject.Parse(await client.GetStringAsync("api/state"));var hidden=state["players"]!.Single();check(hidden["hotbar"]!.Count()==0&&(string?)hidden["portraitStatus"]==""&&(string?)hidden["portraitId"]=="","Public API still clears private hotbar and portrait metadata");
   check((await client.GetAsync($"api/media/{id}?player=p")).StatusCode==HttpStatusCode.NotFound,"Public mode does not bypass player artwork privacy");
  }
 }
}
