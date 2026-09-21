using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using ValheimSagas;
static class MapDetailsHttpChecks {
 public static async Task Run(string root,Action<bool,string> check){
  var socket=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();
  var o=new SagaOptions{DataDirectory=Path.Combine(root,"pins-http"),World="w",ListenPrefix=$"http://127.0.0.1:{port}/",RequireViewerToken=true,ViewerToken="synthetic-map-icon-member-token"};
  using var s=new SagaService(o);s.Start();using var client=new HttpClient{BaseAddress=new Uri(o.ListenPrefix)};
  check((await client.GetAsync("api/map-icons")).StatusCode==HttpStatusCode.Unauthorized&&(await client.GetAsync("api/world-clock")).StatusCode==HttpStatusCode.Unauthorized,"Map icon and clock endpoints obey private host authorization");
  client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",o.ViewerToken);
  var bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=");var id=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  var p=new PlayerSnapshot{World="w",PlayerId="v",ShareMap=true,ShareProfile=false};s.UpdatePlayer(p);s.Explore(new(){World="w",PlayerId="v",Cells=new(){new(){X=0,Z=0}}});
  check(s.UploadMedia(new(){World="w",PlayerId="v",Id=id,Kind="map-icon",Png=bytes}),"Bounded map sprite accepted without a profile capture");
  var pins=new MapPins{World="w",PlayerId="v",Revision="one",Pins=new(){new(){X=2,Z=2,Type="Boss",IconId=id}}};s.UpdatePins(pins);s.UpdateClock(new(){World="w",Day=8,Fraction=.3f});s.Flush();
  check(JObject.Parse(await client.GetStringAsync("api/map-icons"))["pins"]!.Count()==1,"Separate HTTP icon feed contains visible pin");
  check(JObject.Parse(await client.GetStringAsync("api/world-clock"))["clock"]!["day"]!.Value<int>()==8,"Separate HTTP clock feed contains host clock");
  string url="api/media/"+id+"?world=w&player=v";check((await client.GetByteArrayAsync(url)).SequenceEqual(bytes),"Runtime map sprite served independently of profile sharing");
  pins.Revision="personal";pins.Pins[0].Personal=true;s.UpdatePins(pins);s.Flush();check((await client.GetAsync(url)).StatusCode==HttpStatusCode.NotFound,"Unconsented personal sprite not downloadable by guessing its ID");
  p.SharePins=true;s.UpdatePlayer(p);s.Flush();check((await client.GetAsync(url)).IsSuccessStatusCode,"Consented personal sprite can load");
  pins.Revision="outside-personal";pins.Pins[0].X=1000;s.UpdatePins(pins);s.Flush();check((await client.GetAsync(url)).IsSuccessStatusCode,"Consented personal icon is downloadable outside exploration without exposing terrain");
  p.ShareMap=false;s.UpdatePlayer(p);s.Flush();check((await client.GetAsync(url)).StatusCode==HttpStatusCode.NotFound,"Map privacy revocation removes sprite access");
  p.ShareMap=true;s.UpdatePlayer(p);pins.Revision="fog";pins.Pins[0].Personal=false;pins.Pins[0].X=1000;s.UpdatePins(pins);s.Flush();check((await client.GetAsync(url)).StatusCode==HttpStatusCode.NotFound,"Fog-hidden sprite reference does not grant media access");
  pins.Revision="wrong-kind";pins.Pins[0].X=2;s.UpdatePins(pins);s.UploadMedia(new(){World="w",PlayerId="v",Id=id,Kind="portrait",Png=bytes});s.Flush();check((await client.GetAsync(url)).StatusCode==HttpStatusCode.NotFound,"Pin reference cannot turn a private portrait into public map artwork");
 }
}
