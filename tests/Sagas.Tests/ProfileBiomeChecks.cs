using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ValheimSagas;

static class ProfileBiomeChecks {
 static JObject Json(object value)=>JObject.Parse(JsonConvert.SerializeObject(value,new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver()}));
 public static async Task Run(string root,Action<bool,string> check){
  var path=Path.Combine(root,"profile-biomes");var now=DateTime.UtcNow;var all=new TimeWindow(DateTime.MinValue,DateTime.MaxValue);
  var bosses=new[]{("Eikthyr","BlackForest"),("gd_king","Swamp"),("Bonemass","Mountain"),("Dragon(Clone)","Plains"),("GoblinKing","Mistlands"),("SeekerQueen","Ashlands"),("Fader","DeepNorth")};
  string Map(JObject state)=>string.Join("|",state["players"]!.OrderBy(p=>(string)p["playerId"]!).Select(p=>p["playerId"]+":"+p["profileBiome"]+":"+p["profileBiomeEvidence"]));
  string before;
  using(var service=new SagaService(new(){DataDirectory=path,ListenPrefix="",World="theme-world"})){
   void Player(string id,bool shared=true)=>service.Store.Player(new(){World="theme-world",PlayerId=id,Name=id=="private"?"Private Viking":id,ShareProfile=shared,ProfileBiome="Ashlands",ProfileBiomeEvidence="Spoofed private achievement",Gear=new(){new(){Name="Fader trophy"}}});
   foreach(var id in new[]{"team","unknown","fresh","foreign","nonboss"})Player(id);Player("private",false);
   for(int i=0;i<bosses.Length;i++){
    var id="boss-"+i;Player(id);
    service.Store.AddEvent(new(){Id="theme-kill-"+i,World="theme-world",PlayerId=i==1?"private":id,PlayerName=i==1?"Private Viking":id,Kind="kill",Boss=true,Prefab=bosses[i].Item1,Name="Client-localized boss name",Biome="DeepNorth",Utc=now.AddDays(-2),Contributors=new(){id,"team","team","private"}});
   }
   service.Store.AddEvent(new(){Id="modded",World="theme-world",PlayerId="unknown",Kind="kill",Boss=true,Prefab="ModdedFader",Name="Fader",Biome="Ashlands",Utc=now.AddDays(-2)});
   service.Store.AddEvent(new(){Id="not-a-boss",World="theme-world",PlayerId="nonboss",Kind="kill",Boss=false,Prefab="Fader",Utc=now.AddDays(-2)});
   service.Store.AddEvent(new(){Id="other-world",World="other-world",PlayerId="foreign",Kind="kill",Boss=true,Prefab="Fader",Utc=now.AddDays(-2)});
   service.Store.Explore(new(){World="theme-world",PlayerId="fresh",Cells=new(){new(){X=0,Z=0,Biome="Ashlands"}}});
   var state=Json(service.State("theme-world",all));before=Map(state);
   for(int i=0;i<bosses.Length;i++){
    var p=state["players"]!.Single(p=>(string?)p["playerId"]=="boss-"+i);
    check((string?)p["profileBiome"]==bosses[i].Item2,"Boss backdrop advances to next biome, tier "+(i+1));
    check(((string?)p["profileBiomeEvidence"])!.StartsWith("Recorded team victory over "),"Backdrop carries recorded participation evidence, tier "+(i+1));
   }
   var team=state["players"]!.Single(p=>(string?)p["playerId"]=="team");
   check((string?)team["profileBiome"]=="DeepNorth"&&(string?)team["profileBiomeEvidence"]=="Recorded team victory over Fader","Contributor receives highest known boss backdrop, without duplicate-credit effects");
   var elder=state["players"]!.Single(p=>(string?)p["playerId"]=="boss-1");
   check((string?)elder["profileBiome"]=="Swamp"&&!elder["profileBiomeEvidence"]!.ToString().Contains("Private"),"Private finisher does not suppress shared teammate credit or leak their name");
   var privatePlayer=state["players"]!.Single(p=>(string?)p["playerId"]=="private");
   check((string?)privatePlayer["profileBiome"]==""&&(string?)privatePlayer["profileBiomeEvidence"]=="","Private profile clears theme and evidence, including stored spoof");
   foreach(var id in new[]{"fresh","unknown","foreign","nonboss"}){
    var p=state["players"]!.Single(p=>(string?)p["playerId"]==id);
    check((string?)p["profileBiome"]=="Meadows"&&(string?)p["profileBiomeEvidence"]=="Ambient Meadows backdrop; no recorded boss victory","No inferred progression from gear/exploration/modded boss/other world/nonboss: "+id);
   }
   var filtered=Json(service.State("theme-world",new TimeWindow(now.AddMinutes(-30),now),new(){"fresh"}));
   check((int)filtered["stats"]!["kills"]! ==0&&Map(filtered)==before,"Backdrop is independent of dashboard time and selected player filters");
   var oldCustom=Json(service.State("theme-world",new TimeWindow(now.AddDays(-100),now.AddDays(-90))));
   check((int)oldCustom["stats"]!["kills"]! ==0&&Map(oldCustom)==before,"Historic custom end cannot roll a present profile's backdrop backward");
   service.Store.ApplyRetention(now,1,0);
   check(Map(Json(service.State("theme-world",all)))==before,"Retained statistical boss facts preserve contributor themes after detail expiry");
  }
  using(var service=new SagaService(new(){DataDirectory=path,ListenPrefix="",World="theme-world"})){
   check(Map(Json(service.State("theme-world",all)))==before,"Restart rederives identical themes from persistent statistical facts");
   service.Start();check(service.UpdatePlayer(new(){World="theme-world",PlayerId="fresh",Name="fresh",ShareProfile=true,ProfileBiome="Ashlands",ProfileBiomeEvidence="Client says Fader defeated"})&&service.Flush(),"Snapshot update with forged theme is accepted as ordinary telemetry");
   var stored=service.Store.Players("theme-world").Single(p=>p.PlayerId=="fresh");check(stored.ProfileBiome==""&&stored.ProfileBiomeEvidence=="","Incoming client backdrop claims are discarded before storage");
   var fresh=Json(service.State("theme-world",all))["players"]!.Single(p=>(string?)p["playerId"]=="fresh");check((string?)fresh["profileBiome"]=="Meadows"&&!fresh["profileBiomeEvidence"]!.ToString().Contains("Fader"),"Forged snapshot cannot promote query-derived theme");
   var revoked=service.Store.Players("theme-world").Single(p=>p.PlayerId=="team");revoked.ShareProfile=false;service.Store.Player(revoked);
   var hidden=Json(service.State("theme-world",all))["players"]!.Single(p=>(string?)p["playerId"]=="team");check((string?)hidden["profileBiome"]==""&&(string?)hidden["profileBiomeEvidence"]=="","Consent revocation clears previously earned backdrop presentation");
  }
  var web=Path.Combine(root,"profile-biome-web");Directory.CreateDirectory(Path.Combine(web,"biomes"));
  var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6EAAAAABJRU5ErkJggg==");
  var files=new[]{"meadows","black-forest","swamp","mountain","plains","mistlands","ashlands"};
  foreach(var file in files)File.WriteAllBytes(Path.Combine(web,"biomes",file+".png"),png);
  // Synthetic one-pixel WebP transport fixture; these are not production artwork files.
  var webp=Convert.FromBase64String("UklGRiIAAABXRUJQVlA4IBYAAAAwAQCdASoBAAEADsD+JaQAA3AAAAAA");
  var responsive=new List<string>();
  foreach(var composition in new[]{"grounded"}){
   Directory.CreateDirectory(Path.Combine(web,"biomes",composition));
   foreach(var file in files.Concat(new[]{"deep-north"}))foreach(var width in new[]{1920,2560,3840}){
    var name="biomes/"+composition+"/"+file+"-"+width+".webp";responsive.Add(name);File.WriteAllBytes(Path.Combine(web,name),webp);
   }
  }
  foreach(var rejected in new[]{"biomes/scenic/meadows-1920.webp","biomes/scenic/private.webp","biomes/scenic/meadows-4096.webp","biomes/private/meadows-1920.webp"}){
   Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(web,rejected))!);File.WriteAllText(Path.Combine(web,rejected),"DO NOT SERVE");
  }
  File.WriteAllText(Path.Combine(web,"biomes","private.png"),"DO NOT SERVE");File.WriteAllText(Path.Combine(web,"secret.png"),"DO NOT SERVE");
  var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
  using(var service=new SagaService(new(){DataDirectory=Path.Combine(root,"profile-biome-http"),WebDirectory=web,ListenPrefix=$"http://127.0.0.1:{port}/",RequireViewerToken=true,ViewerToken="profile-biome-test-private-token"})){
   service.Start();using var http=new HttpClient{BaseAddress=new Uri($"http://127.0.0.1:{port}/")};
   foreach(var file in files){var response=await http.GetAsync("biomes/"+file+".png");check(response.IsSuccessStatusCode&&response.Content.Headers.ContentType?.MediaType=="image/png"&&(await response.Content.ReadAsByteArrayAsync()).SequenceEqual(png),"Original artwork route is public PNG: "+file);}
   foreach(var file in responsive){
    using var response=await http.GetAsync(file);
    check(response.IsSuccessStatusCode&&response.Content.Headers.ContentType?.MediaType=="image/webp"&&(await response.Content.ReadAsByteArrayAsync()).SequenceEqual(webp)&&response.Headers.CacheControl?.Public==true&&response.Headers.CacheControl.MaxAge==TimeSpan.FromMinutes(5)&&!response.Headers.CacheControl.Extensions.Any(x=>x.Name=="immutable"),"Allowlisted responsive artwork is public WebP with bounded cache: "+file);
   }
   foreach(var rejected in new[]{"biomes/scenic/meadows-1920.webp","biomes/scenic/private.webp","biomes/scenic/meadows-4096.webp","biomes/private/meadows-1920.webp","biomes/SCENIC/meadows-1920.webp","biomes/scenic/%2e%2e/%2e%2e/secret.png","biomes/scenic/%2e%2e/private.png"})
    check((await http.GetAsync(rejected)).StatusCode==HttpStatusCode.NotFound,"Responsive route rejects non-allowlisted or traversed asset: "+rejected);
   using(var response=await http.GetAsync("api/state"))check(response.StatusCode==HttpStatusCode.Unauthorized&&response.Headers.CacheControl?.NoStore==true,"Public decorative cache does not alter private API authorization or cache policy");
   using(var response=await http.GetAsync("api/media/private-image?player=private"))check(response.StatusCode==HttpStatusCode.Unauthorized&&response.Headers.CacheControl?.NoStore==true,"Public decorative cache does not expose private character media");
   File.Delete(Path.Combine(web,"biomes/grounded/ashlands-3840.webp"));check((await http.GetAsync("biomes/grounded/ashlands-3840.webp")).StatusCode==HttpStatusCode.NotFound,"Allowed but missing responsive artwork returns404");
   check((await http.PostAsync("biomes/grounded/meadows-1920.webp",new StringContent("replace"))).StatusCode==HttpStatusCode.MethodNotAllowed,"Responsive artwork routes remain read-only");
   check((await http.GetAsync("api/state")).StatusCode==HttpStatusCode.Unauthorized,"Public backdrop artwork does not make private data public");
   check((await http.GetAsync("biomes/private.png")).StatusCode==HttpStatusCode.NotFound,"Artwork routing has no wildcard PNG exposure");
   check((await http.GetAsync("biomes/%2e%2e/secret.png")).StatusCode==HttpStatusCode.NotFound,"Artwork route cannot traverse to arbitrary files");
   File.Delete(Path.Combine(web,"biomes","ashlands.png"));check((await http.GetAsync("biomes/ashlands.png")).StatusCode==HttpStatusCode.NotFound,"Allowed but missing artwork returns404");
   check((await http.PostAsync("biomes/meadows.png",new StringContent("replace"))).StatusCode==HttpStatusCode.MethodNotAllowed,"Artwork routes remain read-only");
  }
 }
}
