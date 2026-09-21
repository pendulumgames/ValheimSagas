using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace ValheimSagas.Incident;
public static partial class MonoEntry {
 public static void RunHttp(){
  string output=Environment.GetEnvironmentVariable("SAGAS_INCIDENT_OUTPUT")??throw new Exception("Missing isolated output.");if(Directory.Exists(output))throw new Exception("Refusing existing output.");Directory.CreateDirectory(output);
  using(var report=new StreamWriter(Path.Combine(output,"report.txt"))){report.AutoFlush=true;try{
   var art=new byte[4096];if(RuntimeArtPixels.HasVisibleContent(art))throw new Exception("Blank portrait accepted.");for(int i=0;i<512;i+=4){art[i]=200;art[i+3]=255;}if(!RuntimeArtPixels.HasVisibleContent(art))throw new Exception("Visible portrait rejected.");if(RuntimeArtPixels.CanPublishPortrait(false,art)||!RuntimeArtPixels.CanPublishPortrait(true,art))throw new Exception("Portrait body verification gate failed.");report.WriteLine("PASS actual Mono portrait pixel and verified-body gates; GPU capture not exercised");
   var matteBlack=new byte[1024];var matteWhite=new byte[1024];for(int i=0;i<1024;i+=4){matteWhite[i]=matteWhite[i+1]=matteWhite[i+2]=255;}for(int i=128;i<512;i+=4){matteBlack[i]=matteWhite[i]=180;matteBlack[i+1]=matteWhite[i+1]=80;matteBlack[i+2]=matteWhite[i+2]=30;}
   RuntimeArtPixels.ReconstructMatte(matteBlack,matteWhite,true);if(!RuntimeArtPixels.CanPublishPortrait(true,matteBlack)||matteBlack[3]!=0||matteBlack[131]!=255)throw new Exception("RGB-only shader opacity reconstruction failed.");report.WriteLine("PASS actual Mono RGB-only shader matte reconstruction and transparent background (synthetic pixels)");
   var portProbe=new TcpListener(IPAddress.Loopback,0);portProbe.Start();int port=((IPEndPoint)portProbe.LocalEndpoint).Port;portProbe.Stop();
   var prefix="http://127.0.0.1:"+port+"/";const string token="synthetic-mono-http-token-12345";
   using(var service=new SagaService(new SagaOptions{DataDirectory=output,WebDirectory=output,ListenPrefix=prefix,RequireViewerToken=true,ViewerToken=token,World="synthetic",Log=report.WriteLine})){
    service.Start();service.UpdatePlayer(new PlayerSnapshot{World="synthetic",PlayerId="p",Name="Synthetic Mono HTTP",Online=true,ShareMap=true,SharePosition=true,X=32,Z=32});
    service.Explore(new ExplorationBatch{World="synthetic",PlayerId="p",Cells=new System.Collections.Generic.List<MapCell>{new MapCell{X=0,Z=0,Biome="Meadows"}}});
    for(int i=0;i<12;i++)service.TryEvent(new SagaEvent{Id="synthetic-"+i,World="synthetic",PlayerId="p",Kind="kill",Name="Boar",Prefab="Boar",Utc=DateTime.UtcNow,Stars=i%3,X=32,Z=32});
    if(!service.Flush())throw new Exception("Flush timed out.");
    var personal=service.Store.RotatePlayerLogin("synthetic","p");
    if(service.Store.AuthenticatePlayer(personal)?.PlayerId!="p")throw new Exception("Personal login crypto failed.");
    if((string?)Fetch(prefix,"api/session",personal,false)["playerId"]!="p")throw new Exception("Personal login HTTP failed.");
    var rotated=service.Store.RotatePlayerLogin("synthetic","p");Fetch(prefix,"api/session",personal,false,401);Fetch(prefix,"api/session",rotated,false);
    service.Store.RevokePlayerLogin("synthetic","p");Fetch(prefix,"api/session",rotated,false,401);
    report.WriteLine("PASS actual Mono personal login RNG, SHA256 hashing, authenticated session, rotation and revocation");
    if(!(bool)Fetch(prefix,"api/access","",false)["requiresToken"]!)throw new Exception("Private mode discovery incorrect.");
    Fetch(prefix,"api/state","",false,401);
    var state=Fetch(prefix,"api/state?range=all",token,true);if((int)state["stats"]!["kills"]! !=12||!(bool)state["players"]![0]!["online"]!)throw new Exception("Wrong live state.");
    var map=Fetch(prefix,"api/map",token,false);if(map["cells"]!.Count()!=1)throw new Exception("Missing map cell.");
    report.WriteLine("PASS actual Mono HTTP state, live presence, map and gzip negotiation (identity fallback allowed)");
    var rankedPlayer=service.Store.Players("synthetic").Single();rankedPlayer.Gold=123;rankedPlayer.EpicLootInstalled=true;service.UpdatePlayer(rankedPlayer);
    service.UpdatePlayer(new PlayerSnapshot{World="synthetic",PlayerId="q",Name="Synthetic Teammate",ShareProfile=true});
    service.TryEvent(new SagaEvent{Id="synthetic-boss",World="synthetic",PlayerId="p",Kind="kill",Name="Yagluth",Prefab="GoblinKing",Boss=true,Stars=2,DurationSeconds=60,Contributors=new System.Collections.Generic.List<string>{"p","q","q"},Utc=DateTime.UtcNow});
    service.TryEvent(new SagaEvent{Id="synthetic-bounty",World="synthetic",PlayerId="p",Kind="bounty",Name="Synthetic Bounty",Utc=DateTime.UtcNow});
    if(!service.Flush())throw new Exception("Leaderboard flush timed out.");Fetch(prefix,"api/leaderboard","",false,401);
    var boards=Fetch(prefix,"api/leaderboard?range=all",token,false);var boss=boards["bosses"]!.Single(b=>(string?)b["key"]=="GoblinKing");
    if((int)boss["uniquePlayers"]! !=2||boards["fastestBossKills"]!.Count()!=1||(double)boards["fastestBossKills"]![0]!["durationSeconds"]! !=60||(int)boards["gold"]![0]!["value"]! !=123||(int)boards["bounties"]![0]!["value"]! !=1)throw new Exception("Leaderboard team/timing/economy serialization failed.");
    report.WriteLine("PASS actual Mono leaderboard authenticated HTTP, distinct boss team credit, observed duration, carried gold and bounty events");
    var themedState=Fetch(prefix,"api/state?range=30m",token,false);if(themedState["players"]!.Any(p=>(string?)p["profileBiome"]! !="Mistlands"))throw new Exception("Boss contributor backdrop was not derived.");
    var sls=new SlsWorldState{Installed=true,NemesisEnabled=true,ZoneScalingEnabled=true,OverlayEnabled=true,AboveFog=false,Zones=new System.Collections.Generic.List<SlsZone>{new SlsZone{MinX=0,MaxX=64,MinZ=0,MaxZ=64,Level=3,Color="#ffaa00"},new SlsZone{MinX=128,MaxX=192,MinZ=0,MaxZ=64,Level=5,Color="#ff0000"}}};
    if(!service.UpdateSls("synthetic",sls)||!service.Flush())throw new Exception("SLS snapshot queue failed.");
    if(Fetch(prefix,"api/map",token,false)["sls"]!["zones"]!.Count()!=1)throw new Exception("Under-fog zone disclosure failed.");
    sls.AboveFog=true;service.UpdateSls("synthetic",sls);rankedPlayer.NemesisScore=43.75f;service.UpdatePlayer(rankedPlayer);
    service.TryEvent(new SagaEvent{Id="synthetic-nemesis",World="synthetic",PlayerId="p",Kind="kill",Name="Synthetic Nemesis",Prefab="Fader",Boss=true,NemesisBoss=true,Contributors=new System.Collections.Generic.List<string>{"q","q"},Utc=DateTime.UtcNow});
    if(!service.Flush())throw new Exception("Nemesis flush failed.");
    if(Fetch(prefix,"api/map",token,false)["sls"]!["zones"]!.Count()!=2)throw new Exception("Above-fog host setting not honored.");
    var nemesisState=Fetch(prefix,"api/state",token,false);if((float)nemesisState["players"]!.Single(p=>(string?)p["playerId"]=="p")["nemesisScore"]! !=43.75f||(int)nemesisState["stats"]!["nemesisBossKills"]! !=1)throw new Exception("Nemesis snapshot/metric failed.");
    var nemesisBoard=Fetch(prefix,"api/leaderboard",token,false);if(nemesisBoard["nemesisBossKills"]!.Count()!=2||nemesisBoard["progression"]!.Any(p=>(string?)p["highestBoss"]=="Fader"))throw new Exception("Nemesis team/progression isolation failed.");
    report.WriteLine("PASS actual Mono SLS snapshot, fog selection, Nemesis score, team metric and vanilla progression isolation (synthetic data)");
    Directory.CreateDirectory(Path.Combine(output,"biomes"));var backdrop=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=");File.WriteAllBytes(Path.Combine(output,"biomes","meadows.png"),backdrop);
    var imageRequest=(HttpWebRequest)WebRequest.Create(prefix+"biomes/meadows.png");imageRequest.Proxy=null;imageRequest.Timeout=10000;using(var imageResponse=(HttpWebResponse)imageRequest.GetResponse()){using(var imageStream=imageResponse.GetResponseStream()){using(var data=new MemoryStream()){imageStream.CopyTo(data);if(imageResponse.ContentType!="image/png"||!data.ToArray().SequenceEqual(backdrop))throw new Exception("Public decorative PNG routing failed.");}}}
    report.WriteLine("PASS actual Mono lifetime boss-theme projection and public allowlisted PNG bytes/MIME");
    Directory.CreateDirectory(Path.Combine(output,"biomes","grounded"));var responsiveArt=Convert.FromBase64String("UklGRiIAAABXRUJQVlA4IBYAAAAwAQCdASoBAAEADsD+JaQAA3AAAAAA");File.WriteAllBytes(Path.Combine(output,"biomes","grounded","meadows-3840.webp"),responsiveArt);
    var webpRequest=(HttpWebRequest)WebRequest.Create(prefix+"biomes/grounded/meadows-3840.webp");webpRequest.Proxy=null;webpRequest.Timeout=10000;
    using(var webpResponse=(HttpWebResponse)webpRequest.GetResponse())using(var webpStream=webpResponse.GetResponseStream())using(var data=new MemoryStream()){webpStream.CopyTo(data);if(webpResponse.ContentType!="image/webp"||webpResponse.Headers["Cache-Control"]!="public, max-age=300"||!data.ToArray().SequenceEqual(responsiveArt))throw new Exception("Public responsive WebP routing/cache failed.");}
    report.WriteLine("PASS actual Mono responsive WebP public MIME, bounded cache and exact synthetic bytes");
    // A valid tiny PNG with a large ignored ancillary chunk exercises multi-packet media on installed Mono.
    var padding=new byte[160000];new Random(29).NextBytes(padding);var largePng=new byte[backdrop.Length+padding.Length+12];int split=backdrop.Length-12;
    Array.Copy(backdrop,largePng,split);int position=split;
    Action<uint> write32=n=>{largePng[position++]=(byte)(n>>24);largePng[position++]=(byte)(n>>16);largePng[position++]=(byte)(n>>8);largePng[position++]=(byte)n;};
    write32((uint)padding.Length);var tag=new byte[]{115,97,71,97};Array.Copy(tag,0,largePng,position,4);position+=4;Array.Copy(padding,0,largePng,position,padding.Length);position+=padding.Length;
    uint crc=0xffffffff;foreach(var b in tag.Concat(padding)){crc^=b;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}write32(crc^0xffffffff);Array.Copy(backdrop,split,largePng,position,12);
    string largeId;using(var hash=System.Security.Cryptography.SHA256.Create())largeId=BitConverter.ToString(hash.ComputeHash(largePng)).Replace("-","").ToLowerInvariant();
    var largeMedia=new MediaUpload{World="synthetic",PlayerId="p",Kind="portrait",Id=largeId,Png=largePng};var assembly=new MediaTransfer();MediaUpload? assembled=null;
    for(int chunk=MediaTransfer.ChunkCount(largePng.Length)-1;chunk>=0;chunk--)assembled=assembly.Accept("p","synthetic",MediaTransfer.Slice(largeMedia,chunk),1);
    if(assembled==null||!assembled.Png.SequenceEqual(largePng)||!service.UploadMedia(assembled))throw new Exception("Large media chunk assembly failed.");
    rankedPlayer.PortraitId=largeId;service.UpdatePlayer(rankedPlayer);if(!service.Flush())throw new Exception("Large media flush failed.");
    var mediaRequest=(HttpWebRequest)WebRequest.Create(prefix+"api/media/"+largeId+"?world=synthetic&player=p");mediaRequest.Proxy=null;mediaRequest.Timeout=10000;mediaRequest.Headers["Authorization"]="Bearer "+token;
    using(var mediaResponse=(HttpWebResponse)mediaRequest.GetResponse())using(var mediaStream=mediaResponse.GetResponseStream())using(var mediaData=new MemoryStream()){mediaStream.CopyTo(mediaData);if(mediaResponse.ContentType!="image/png"||!mediaData.ToArray().SequenceEqual(largePng))throw new Exception("Large portrait HTTP bytes changed.");}
    report.WriteLine("PASS actual Mono bounded portrait chunk reassembly, SHA256, LiteDB persistence and authenticated full PNG HTTP delivery (synthetic ancillary payload)");
    service.Store.SavePersonalLore("synthetic","p",new PersonalLoreSettings{Active="Synthetic profile",Presets=new System.Collections.Generic.List<LorePreset>{new LorePreset{Name="Synthetic profile"}},Key="synthetic-mono-key-only"});
    if(service.Store.PersonalLore("synthetic","p",true).Key!="synthetic-mono-key-only")throw new Exception("Mono personal key encryption roundtrip failed.");
    report.WriteLine("PASS actual Mono AES/HMAC encrypted personal storyteller key roundtrip");
    service.Store.Chapter(new SagaChapter{Id="synthetic-server-chapter",Model="fixture/generated",Scope="server",World="synthetic",Utc=DateTime.UtcNow,Title="Synthetic shared story",Text="Fictional embellishment: a synthetic server chapter for the isolated runtime check.",Participants=new System.Collections.Generic.List<SagaParticipant>{new SagaParticipant{PlayerId="p",Name="Synthetic Mono HTTP"}},EventIds=new System.Collections.Generic.List<string>{"synthetic-0"}});
    Fetch(prefix,"api/server-saga","",false,401);
    var shared=Fetch(prefix,"api/server-saga",token,false);
    if(shared["chapters"]!.Count()!=1||(string?)shared["chapters"]![0]!["scope"]! !="server")throw new Exception("Missing shared chapter.");
    var hidden=service.Store.Players("synthetic").Single(p=>p.PlayerId=="p");hidden.ShareProfile=false;service.UpdatePlayer(hidden);if(!service.Flush())throw new Exception("Privacy flush timed out.");
    if(Fetch(prefix,"api/server-saga",token,false)["chapters"]!.Any())throw new Exception("Revoked participant leaked through server saga.");
    if(service.Store.ServerChapters("synthetic").Count!=1)throw new Exception("Privacy revocation destroyed persistent history.");
    var privateBoard=Fetch(prefix,"api/leaderboard",token,false);if(privateBoard["gold"]!.Any()||privateBoard["bounties"]!.Any()||privateBoard["fastestBossKills"]![0]!["team"]!.Count()!=1||privateBoard["fastestBossKills"]![0]!["finisher"]!.Type!=JTokenType.Null)throw new Exception("Leaderboard privacy revocation failed.");
    report.WriteLine("PASS actual Mono leaderboard removes private player metrics and team identity while retaining shared teammate credit");
    var hiddenTheme=Fetch(prefix,"api/state",token,false)["players"]!.Single(p=>(string?)p["playerId"]=="p");if((string?)hiddenTheme["profileBiome"]! !=""||(string?)hiddenTheme["profileBiomeEvidence"]! !="")throw new Exception("Private profile backdrop leaked.");
    report.WriteLine("PASS actual Mono private profile backdrop/evidence removal");
    report.WriteLine("PASS actual Mono Server Saga serialization, authentication and participant privacy revocation");
    service.Store.Dispose();var failure=Fetch(prefix,"api/state?range=all",token,false,500);if(failure["errorId"]==null)throw new Exception("Missing failure correlation.");
    Fetch(prefix,"api/health",token,false);report.WriteLine("PASS actual Mono JSON500 correlation and independent health endpoint");
   }
   using(var publicService=new SagaService(new SagaOptions{DataDirectory=Path.Combine(output,"public"),ListenPrefix=prefix,ViewerToken="",RequireViewerToken=false,World="synthetic-public",Log=report.WriteLine})){
    publicService.Start();if((bool)Fetch(prefix,"api/access","",false)["requiresToken"]!)throw new Exception("Public access discovery incorrect.");
    Fetch(prefix,"api/state","",false);Fetch(prefix,"api/map","",false);Fetch(prefix,"api/server-saga","",false);Fetch(prefix,"api/leaderboard","",false);
    report.WriteLine("PASS actual Mono public mode without a token, private rejection and access discovery");
   }
  }catch(Exception e){report.WriteLine(e);throw;}}
 }
 static JObject Fetch(string prefix,string path,string token,bool gzip,int expected=200){var request=(HttpWebRequest)WebRequest.Create(prefix+path);request.Proxy=null;request.Timeout=10000;if(token!="")request.Headers["Authorization"]="Bearer "+token;if(gzip)request.Headers["Accept-Encoding"]="gzip";HttpWebResponse response;
  try{response=(HttpWebResponse)request.GetResponse();}catch(WebException e){if(!(e.Response is HttpWebResponse errorResponse))throw;response=errorResponse;}
  using(response){if((int)response.StatusCode!=expected)throw new Exception("Unexpected HTTP "+response.StatusCode);using(var raw=response.GetResponseStream()){using(var input=response.ContentEncoding=="gzip"?(Stream)new GZipStream(raw,CompressionMode.Decompress):raw){using(var reader=new StreamReader(input))return JObject.Parse(reader.ReadToEnd());}}}
 }
}
