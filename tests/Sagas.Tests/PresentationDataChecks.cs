using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using ValheimSagas;
static class PresentationDataChecks {
 static MapCell Tile(int x,int z,params int[] pixels){var bytes=new byte[TerrainTile.ByteCount];foreach(var pixel in pixels){bytes[pixel*4]=42;bytes[pixel*4+1]=120;bytes[pixel*4+2]=68;bytes[pixel*4+3]=255;}return new(){X=x,Z=z,TerrainPixels=Convert.ToBase64String(bytes)};}
 public static async Task Run(string root,Action<bool,string> check){
  var a=Tile(-1,-1,0);var b=Tile(-1,-1,1);
  check(TerrainTile.Valid(a.TerrainPixels)&&!TerrainTile.Valid("not-base64"),"Terrain validates exact encoded tile size");
  var bytes=Convert.FromBase64String(a.TerrainPixels);bytes[7]=128;check(!TerrainTile.Valid(Convert.ToBase64String(bytes)),"Terrain rejects partially transparent terrain");bytes[7]=0;bytes[4]=12;check(!TerrainTile.Valid(Convert.ToBase64String(bytes)),"Fog cannot contain hidden RGB terrain");
  check(TerrainTile.Known(a,-64,-64)&&TerrainTile.Known(a,-60.01f,-60.01f)&&!TerrainTile.Known(a,-60,-64)&&!TerrainTile.Known(a,-64.01f,-64),"Negative terrain coordinates obey exact four-meter subcell boundaries");
  var union=TerrainTile.Union(new[]{a,b});check(TerrainTile.Known(union,-64,-64)&&TerrainTile.Known(union,-60,-64)&&!TerrainTile.Known(union,-56,-64),"Combined exploration merges complementary known pixels without exposing fog");
  var legacy=new MapCell{X=-1,Z=-1};check(TerrainTile.Valid(legacy.TerrainPixels)&&TerrainTile.Known(legacy,-56,-64)&&TerrainTile.Union(new[]{a,legacy}).TerrainPixels=="","Legacy fully known tiles preserve fallback");
  var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=");
  string Hash(byte[] data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
  MediaUpload Upload(byte[] data)=>new(){World="presentation",PlayerId="shared",Id=Hash(data),Kind="icon",Png=data};
  var media=Upload(png);check(SagaService.ValidMedia(media),"Synthetic one-pixel PNG accepted with content hash");
  var bad=Upload(png);bad.Id=new string('0',64);check(!SagaService.ValidMedia(bad),"Media hash must match bytes");
  var large=new byte[48*1024+1];png.CopyTo(large,0);check(!SagaService.ValidMedia(Upload(large)),"Media beyond 48 KiB rejected");
  var wide=png.ToArray();wide[18]=2;wide[19]=1;check(!SagaService.ValidMedia(Upload(wide)),"Media dimension beyond 512 pixels rejected");
  var probe=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
  var options=new SagaOptions{DataDirectory=Path.Combine(root,"presentation"),World="presentation",RequireViewerToken=true,ViewerToken="synthetic-artwork-test-token",ListenPrefix=$"http://127.0.0.1:{port}/",ServerName="Synthetic Fjord",ServerAddress="test.invalid:2456",LoreMilestoneEvents=99999};
  using var service=new SagaService(options);service.Start();
  var shared=new PlayerSnapshot{World=options.World,PlayerId="shared",Name="Synthetic Viking",ShareProfile=true,ShareMap=true,PortraitId=media.Id};
  service.UpdatePlayer(shared);service.UpdatePlayer(new(){World=options.World,PlayerId="second",Name="Synthetic Second",ShareMap=true});service.UpdatePlayer(new(){World=options.World,PlayerId="private",Name="Synthetic Private",ShareMap=false,ShareProfile=false,PortraitId=media.Id});
  service.Explore(new(){World=options.World,PlayerId="shared",Cells=new(){a}});service.Explore(new(){World=options.World,PlayerId="second",Cells=new(){b}});service.Explore(new(){World=options.World,PlayerId="private",Cells=new(){Tile(-1,-1,2)}});
  service.UploadMedia(media);service.TryEvent(new(){Id="known",World=options.World,PlayerId="shared",X=-64,Z=-64});service.TryEvent(new(){Id="fogged",World=options.World,PlayerId="shared",X=-56,Z=-64});check(service.Flush(),"Presentation writes committed");
  var map=JObject.FromObject(service.Map(options.World));var mapTile=map["cells"]!.First()!.ToObject<MapCell>()!;
  check(TerrainTile.Known(mapTile,-60,-64)&&!TerrainTile.Known(mapTile,-56,-64),"API combined map excludes private discoveries");
  var personal=JObject.FromObject(service.Map(options.World,new(){"shared"}))["cells"]!.First()!.ToObject<MapCell>()!;
  check(!TerrainTile.Known(personal,-60,-64)&&JObject.FromObject(service.Map(options.World,new(){"private"}))["cells"]!.Count()==0,"Personal map selection preserves exploration privacy");
  var state=JObject.FromObject(service.State(options.World,new(DateTime.MinValue,DateTime.MaxValue)));
  check(state["events"]!.Single(e=>(string?)e["Id"]=="fogged")["X"]!.Type==JTokenType.Null&&state["events"]!.Single(e=>(string?)e["Id"]=="known")["X"]!.Type!=JTokenType.Null,"Heat coordinates hidden behind exact subcell fog");
  check((string?)state["server"]!["name"]==options.ServerName&&(string?)state["server"]!["address"]==options.ServerAddress,"Server identity returned explicitly");
  using var client=new HttpClient{BaseAddress=new Uri(options.ListenPrefix)};string url=$"api/media/{media.Id}?player=shared";
  check((await client.GetAsync(url)).StatusCode==HttpStatusCode.Unauthorized,"Artwork requires authenticated viewer");client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",options.ViewerToken);
  var response=await client.GetAsync(url);check(response.IsSuccessStatusCode&&response.Content.Headers.ContentType?.MediaType=="image/png"&&(await response.Content.ReadAsByteArrayAsync()).SequenceEqual(png),"Authenticated portrait returns exact PNG bytes");
  check((await client.GetAsync($"api/media/{media.Id}?player=second")).StatusCode==HttpStatusCode.NotFound&&(await client.GetAsync($"api/media/{media.Id}?player=private")).StatusCode==HttpStatusCode.NotFound,"Artwork cannot be requested for another or private player");
  shared.PortraitId="";shared.Gear.Add(new(){IconId=media.Id});service.UpdatePlayer(shared);service.Flush();check((await client.GetAsync(url)).IsSuccessStatusCode,"Referenced gear icon remains available without portrait reference");
  shared.ShareProfile=false;service.UpdatePlayer(shared);service.Flush();check((await client.GetAsync(url)).StatusCode==HttpStatusCode.NotFound,"Profile privacy revocation immediately gates stored artwork");
  shared.ShareProfile=true;shared.Gear.Clear();service.UpdatePlayer(shared);service.Flush();check((await client.GetAsync(url)).StatusCode==HttpStatusCode.NotFound,"Unreferenced cached artwork remains inaccessible");
 }
}
