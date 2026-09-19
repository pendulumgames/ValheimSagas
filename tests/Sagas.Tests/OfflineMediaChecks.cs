using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using ValheimSagas;

static class OfflineMediaChecks {
 static string Prefix(){var probe=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();return $"http://127.0.0.1:{port}/";}
 public static async Task Run(string root,Action<bool,string> check){
  var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=");
  var id=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
  var options=new SagaOptions{DataDirectory=Path.Combine(root,"offline-media"),ListenPrefix=Prefix(),World="offline-world",RequireViewerToken=false,LoreMilestoneEvents=99999};
  var p=new PlayerSnapshot{World=options.World,PlayerId="departed",Name="Synthetic Departed Viking",Online=true,ShareProfile=true,PortraitId=id,Gear=new(){new(){Name="Synthetic shield",IconId=id}},Hotbar=new(){new(){Name="Synthetic shield",IconId=id,HotbarSlot=2}}};
  string Url(string player="departed",string world="offline-world")=>$"api/media/{id}?player={player}&world={world}";
  async Task Verify(HttpClient client,string phase){
   var state=JObject.Parse(await client.GetStringAsync("api/state?range=30m"));
   var player=state["players"]!.Single(x=>(string?)x["playerId"]==p.PlayerId);
   check(!(bool)player["online"]! && player["gear"]!.Count()==1 && player["hotbar"]!.Count()==1,$"{phase}: offline current gear survives event time filter");
   check((string?)player["portraitId"]==id && (string?)player["gear"]![0]!["iconId"]==id && (string?)player["hotbar"]![0]!["iconId"]==id,$"{phase}: all last-known media references retained");
   using var response=await client.GetAsync(Url());
   check(response.IsSuccessStatusCode && (await response.Content.ReadAsByteArrayAsync()).SequenceEqual(png),$"{phase}: offline artwork served from persistent storage to a fresh HTTP client");
  }
  using(var service=new SagaService(options)){
   service.Start();service.UpdatePlayer(p);service.UpdatePlayer(new(){World=options.World,PlayerId="remaining",Name="Synthetic Remaining Viking",Online=true});
   service.UploadMedia(new(){World=options.World,PlayerId=p.PlayerId,Id=id,Kind="icon",Png=png});service.Flush();
   service.SetOffline(options.World,p.PlayerId);check(service.Flush(),"Player logout committed before media check");
   using var client=new HttpClient{BaseAddress=new Uri(options.ListenPrefix)};await Verify(client,"After logout with another player online");
   check((await client.GetAsync(Url("remaining"))).StatusCode==HttpStatusCode.NotFound && (await client.GetAsync(Url(world:"another-world"))).StatusCode==HttpStatusCode.NotFound,"Offline art remains scoped to player and world");
  }
  options.ListenPrefix=Prefix();
  using(var service=new SagaService(options)){
   service.Start();using var client=new HttpClient{BaseAddress=new Uri(options.ListenPrefix)};await Verify(client,"After host restart");
   p.Online=false;p.ShareProfile=false;service.UpdatePlayer(p);service.Flush();
   check((await client.GetAsync(Url())).StatusCode==HttpStatusCode.NotFound,"Offline media respects explicit profile privacy revocation");
   p.ShareProfile=true;service.UpdatePlayer(p);service.Flush();
   check((await client.GetAsync(Url())).IsSuccessStatusCode,"Restoring profile sharing recovers persisted media without another client upload");
  }
 }
}
