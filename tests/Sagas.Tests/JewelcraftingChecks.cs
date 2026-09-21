using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using ValheimSagas;
static class JewelcraftingChecks {
 public static async Task Run(string root,Action<bool,string> check){
  var probe=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
  var options=new SagaOptions{DataDirectory=Path.Combine(root,"jewel"),ListenPrefix=$"http://127.0.0.1:{port}/",World="jewel",RequireViewerToken=false,LoreEnabled=false};
  var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=");var id=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
  var sockets=new List<GemSocket>{new(){Name="Synthetic Ruby",Prefab="SyntheticGem",IconId=id,Effects=new(){"Power: 1–3 (configured range)"}},new()};
  var gear=new GearItem{Name="Synthetic sword",Equipped=true,Rarity="Epic",RarityColor="#9900ff",Effects=new(){"Synthetic Epic Loot effect"},Sockets=sockets,SocketColor="#eeaa33"};
  var player=new PlayerSnapshot{World="jewel",PlayerId="p",Name="Synthetic Jeweler",ShareProfile=true,JewelcraftingInstalled=true,Gear=new(){gear}};
  using(var service=new SagaService(options)){
   service.Start();check(service.UpdatePlayer(player),"Jewelcrafting and Epic Loot metadata accepted together");service.UploadMedia(new(){World="jewel",PlayerId="p",Id=id,Kind="icon",Png=png});service.Flush();
   check(service.Store.NarrativeContext("jewel","p").Facts.Any(f=>f.Contains("Jewelcrafting sockets")&&f.Contains("Synthetic Ruby")),"Socket snapshot names included in narrative context without a provider call");
   var board=JObject.FromObject(service.Leaderboard("jewel",new(DateTime.MinValue,DateTime.MaxValue)));
   check((bool)board["availability"]!["jewelcraftingAvailable"]!&&!(bool)board["availability"]!["epicLootAvailable"]!,"Jewelcrafting-only detection does not imply Epic Loot");
   check((long)board["equippedGems"]![0]!["Value"]! ==1 && (long)board["equippedSockets"]![0]!["Value"]! ==2,"Latest loadout counts filled gems separately from capacity");
   using var client=new HttpClient{BaseAddress=new Uri(options.ListenPrefix)};
   check((await client.GetAsync($"api/media/{id}?world=jewel&player=p")).IsSuccessStatusCode,"Gem-only artwork reference authorizes opted-in media");
   check((await client.GetAsync($"api/media/{id}?world=other&player=p")).StatusCode==HttpStatusCode.NotFound,"Gem artwork isolated by world");
   var drop=new SagaEvent{Id="drop",World="jewel",PlayerId="p",Kind="drop",Provenance="origin",Name=gear.Name,Sockets=sockets,SocketColor=gear.SocketColor,Rarity=gear.Rarity,Effects=gear.Effects};
   var collect=new SagaEvent{Id="collect",World="jewel",PlayerId="p",Kind="collect",Provenance="origin"};
   check(service.Store.AddEvent(collect),"Early collection queued");check(service.Store.AddEvent(drop),"Socketed actual drop accepted");check(!service.Store.AddEvent(drop),"Repeated Jewelcrafting death drop deduplicated");
   var received=service.Store.Events("jewel",new(DateTime.MinValue,DateTime.MaxValue)).Single(e=>e.Id=="collect");
   check(received.Sockets.Count==2&&received.Sockets[0].Name=="Synthetic Ruby"&&received.Rarity=="Epic"&&received.Effects.Count==1,"Provenance reconciliation preserves both mods and empty slots");
   check(!service.Store.AddEvent(new(){Id="extra",World="jewel",PlayerId="p",Kind="collect",Provenance="origin"}),"No second earned collection beyond actual dropped amount");
   gear.Sockets=Enumerable.Range(0,12).Select(_=>new GemSocket()).ToList();check(!service.UpdatePlayer(player),"Oversized socket snapshot rejected");gear.Sockets=sockets;
   sockets[0].IconId="bad";check(!service.UpdatePlayer(player),"Invalid gem media ID rejected");sockets[0].IconId=id;
   sockets[0].Effects.Add(new string('x',241));check(!service.UpdatePlayer(player),"Oversized gem effect rejected");sockets[0].Effects.RemoveAt(1);
   service.SetOffline("jewel","p");service.Flush();
  }
  using(var service=new SagaService(options)){
   service.Start();var saved=service.Store.Players("jewel").Single(p=>p.PlayerId=="p");check(saved.Gear[0].Sockets.Count==2&&saved.Gear[0].Effects.Count==1,"Sockets and Epic effects survive restart/offline");
   using var client=new HttpClient{BaseAddress=new Uri(options.ListenPrefix)};
   check((await client.GetAsync($"api/media/{id}?world=jewel&player=p")).IsSuccessStatusCode,"Last-known gem icon survives logout/restart");
   saved.ShareProfile=false;service.UpdatePlayer(saved);service.Flush();
   check((await client.GetAsync($"api/media/{id}?world=jewel&player=p")).StatusCode==HttpStatusCode.NotFound,"Revoking profile sharing revokes gem artwork");
   var privateBoard=JObject.Parse(await client.GetStringAsync("api/leaderboard?world=jewel&range=all"));
   check(!(bool)privateBoard["availability"]!["jewelcraftingAvailable"]!&&!privateBoard["equippedGems"]!.Any(),"Private profiles do not reveal mod detection or equipped-gem rankings");
   var state=JObject.Parse(await client.GetStringAsync("api/state"));check(state["players"]![0]!["gear"]!.Count()==0,"Private sockets are not exposed in state");check(!(bool)state["players"]![0]!["jewelcraftingInstalled"]!,"Private Jewelcrafting flag redacted from state");
  }
 }
}

