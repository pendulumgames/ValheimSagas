using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
namespace ValheimSagas;

[BepInPlugin("org.valheimsagas.collector", "Valheim Sagas", "0.3.23")]
[BepInDependency("_shudnal.ConfigurationManager", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("randyknapp.mods.epicloot", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("MidnightsFX.StarLevelSystem", BepInDependency.DependencyFlags.SoftDependency)]
public sealed partial class SagasPlugin : BaseUnityPlugin {
 internal static SagasPlugin? Instance;
 internal static string World => ZNet.instance ? ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture) : "";
 internal static string Identity(long id) { if(id==0)return ""; using var sha=SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(World+":"+id))).Replace("-", "").ToLowerInvariant().Substring(0,24); }
 internal static string EventId(string kind,ZDOID zdo) {using var sha=SHA256.Create();return kind+":"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(World+":"+zdo))).Replace("-", "").ToLowerInvariant();}
 internal static string Localize(string value) => Localization.instance == null ? value : Localization.instance.Localize(value);
 RuntimeArt? artwork; readonly Dictionary<string,Packet> pendingMedia=new Dictionary<string,Packet>(); readonly Dictionary<string,float> sentMedia=new Dictionary<string,float>(); readonly HashSet<string> fullyMapped=new HashSet<string>(); readonly Dictionary<string,string> tileVersions=new Dictionary<string,string>(); ConfigEntry<bool> notifications=null!; ConfigEntry<string> serverName=null!,serverAddress=null!;
 SagaService? service; readonly AsyncResource<StartedService> serviceStartup=new AsyncResource<StartedService>(); float nextServiceStart; Harmony? harmony; Outbox? outbox;
 readonly Dictionary<string,Packet> pendingMaps=new Dictionary<string,Packet>();
 ConfigEntry<bool> requireToken=null!,host=null!, shareMap=null!, sharePosition=null!,shareProfile=null!;
 ConfigEntry<string> model=null!; ConfigEntry<int> daily=null!,cooldown=null!,milestones=null!,retention=null!,statisticsRetention=null!;
 ConfigEntry<string> data=null!, prefix=null!, token=null!, key=null!; ConfigEntry<bool> lore=null!,allowPaidLore=null!;
 readonly HashSet<string> knownCharacters=new HashSet<string>();
 readonly HashSet<ZRpc> registered=new HashSet<ZRpc>(); readonly Dictionary<long,string> online=new Dictionary<long,string>();
 readonly Dictionary<string,SagaEvent> pending=new Dictionary<string,SagaEvent>();
 readonly Dictionary<ZRpc,(float start,int count)> rates=new Dictionary<ZRpc,(float,int)>();
 readonly HashSet<string> sentCells=new HashSet<string>(); int mapCursor; bool importing=true; float nextTick; string activeWorld=""; string lastLocalId=""; string journalId=""; int retryCursor;
 readonly ConcurrentQueue<Action> committed=new ConcurrentQueue<Action>();
 readonly Dictionary<string,float> nextWarning=new Dictionary<string,float>();
 string positionLogKey="";int startupTraceTicks;bool profileTraceRecorded;

 static readonly System.Reflection.FieldInfo exploredField=AccessTools.Field(typeof(Minimap),"m_explored");
 const string RpcName="Sagas.V1", AckName="Sagas.Ack.V1";
 readonly MediaTransfer mediaTransfer=new MediaTransfer();
 readonly Dictionary<string,int> mediaCursors=new Dictionary<string,int>();
 readonly TelemetryBudget telemetryBudget=new TelemetryBudget();
 readonly Dictionary<string,float> retryAfter=new Dictionary<string,float>();
 Packet? outboundProfile; float nextUpload; int uploadLane;
 void Awake() {
  Instance=this; SetupMapDetails(); SetupLogin(); SetupWebsiteOverlay(); requireToken=BindSetting("Server","RequireViewerToken",false,"True: private viewing requires ViewerToken. False: anyone who can reach the website may view shared data without a token. Does not change network binding or player sharing preferences."); artwork=new RuntimeArt(Warn); notifications=BindSetting("Notifications","EnableLootNotifications",false,"Show Sagas rare earned-loot pickup messages. Does not change Valheim or Epic Loot notifications."); serverName=BindSetting("Server","DisplayName","","Website server name override; blank uses the Valheim server name, then the world name."); serverAddress=BindSetting("Server","AdvertisedAddress","","Optional server IP/hostname and port displayed to website viewers. No automatic public-IP discovery.");
  host=BindSetting("Server","EnableWebsite",true,"Start the private HTTP service only when hosting a world.");
  data=BindSetting("Server","DataDirectory",Path.Combine(Paths.ConfigPath,"ValheimSagas"),"Persistent database path; back up separately from world saves.");
  prefix=BindSetting("Server","ListenPrefix","http://127.0.0.1:8877/","Loopback by default. Use HTTPS reverse proxy for remote access.");
  token=BindSetting("Server","ViewerToken",Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N"),"Private member credential; never synced to game clients.");
  lore=BindSetting("Lore","EnableOpenRouter",true,"Send selected narrative facts to OpenRouter. Free routing by default; paid models require AllowPaidModels. No coordinates or account IDs.");
  key=BindSetting("Lore","OpenRouterKey","","Server only. Prefer OPENROUTER_API_KEY environment variable.");
  model=BindSetting("Lore","Model","openrouter/free","OpenRouter model ID or @preset/name from the key owner account. Defaults to openrouter/free. Paid routes require AllowPaidModels. Manage pricing/provider limits in your OpenRouter preset or account.");
  allowPaidLore=BindSetting("Lore","AllowPaidModels",false,"Allow the host key to pay for personal and server sagas using Model. False enforces zero token prices, including presets. Restart host after changing.");
  daily=BindSetting("Lore","DailyBudget",20,"Maximum external requests per UTC day.");
  cooldown=BindSetting("Lore","CooldownMinutes",180,"Minimum chapter interval per character.");
  milestones=BindSetting("Lore","MilestoneEvents",20,"Ordinary event count before a chapter; notable events may qualify earlier.");
  retention=BindSetting("Server","RetentionDays",0,"Zero retains all detailed history. Positive values prune old events; dedup ledger remains.");
  statisticsRetention=BindSetting("Server","StatisticsRetentionDays",0,"Zero keeps exact statistical facts forever. Positive values must be at least RetentionDays; older facts are deleted, dedup lineage remains.");
  shareProfile=BindSetting("Privacy","ShareProfile",true,"Share statistics, gear and saga with website viewers.");
  shareMap=BindSetting("Privacy","ShareMap",true,"Share personal exploration with website viewers.");
  // Installed shudnal ConfigurationManager supports standard DisplayNameAttribute
  // and the Advanced tag. No runtime dependency on that optional manager.
  sharePosition=BindSetting("Privacy","SharePosition",true,new ConfigDescription(
   "Website permission, normally left on. Use 'Visible to other players' on Valheim's map as your everyday position-sharing control. Turn this permission off only to hide your position from the website while still sharing it in-game. Existing choices are preserved.",
   null,"Advanced",new System.ComponentModel.DisplayNameAttribute("Allow website position sharing")));
  harmony=new Harmony("org.valheimsagas.collector"); harmony.PatchAll(typeof(SagasPlugin).Assembly); SetupConfigurationManager();
  Logger.LogInfo("Valheim Sagas loaded; telemetry hooks installed. No external map dependency.");
 }
 sealed class StartedService:IDisposable {internal SagaService Service=null!;internal string[] Characters=Array.Empty<string>();internal double Milliseconds;public void Dispose()=>Service.Dispose();}
 void StopService(){ResetMapDetails();slsCapture?.Dispose();slsCapture=null;var ids=online.Values.Concat(new[]{lastLocalId}).Where(x=>x!="").Distinct().ToArray();var world=activeWorld;service=null;lastLocalId="";serviceStartup.Retire(started=>{foreach(var id in ids)started.Service.SetOffline(world,id);});}
 void OnDestroy() { artwork?.Clear(); GuardLogin(ClearLoginClipboard); RuntimeTerrain.Clear(); outbox?.Finish(pending.Values.ToArray());StopService(); harmony?.UnpatchSelf(); Instance=null; }
 void Update() {
  GuardLogin(UpdateLogin);
  GuardLogin(UpdateWebsiteOverlay);
  artwork?.PumpPortrait(Player.m_localPlayer,shareProfile.Value,World);
  RunStage("terrain preparation",RuntimeTerrainShader.Pump);
  if(!RuntimeTerrainShader.Pending)RunStage("item icon capture",()=>artwork?.PumpIcons(shareProfile.Value));
  while(committed.TryDequeue(out var action)) {try{action();}catch{}}
  if(Time.unscaledTime>=nextTick){nextTick=Time.unscaledTime+3;RunStage("world update",Tick);}
  RunStage("SLS capture slice",RefreshSls);
  RunStage("map pin capture slice",PumpMapDetails);
  if(Time.unscaledTime>=nextUpload){nextUpload=Time.unscaledTime+.1f;RunStage("paced uploads",PumpUploads);}
 }
 void Warn(string stage,Exception e) {if(nextWarning.TryGetValue(stage,out var next)&&Time.unscaledTime<next)return;nextWarning[stage]=Time.unscaledTime+60;Logger.LogWarning("Sagas "+stage+" failed (repeated errors suppressed for 60 seconds): "+e);}
 void RunStage(string stage,Action action) {var start=System.Diagnostics.Stopwatch.GetTimestamp();try{action();}catch(Exception e){Warn(stage,e);}finally{var ms=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency;if(ms>=50&&(!nextWarning.TryGetValue("slow "+stage,out var next)||Time.unscaledTime>=next)){nextWarning["slow "+stage]=Time.unscaledTime+60;Logger.LogWarning("Sagas slow stage: "+stage+" took "+ms.ToString("F1",CultureInfo.InvariantCulture)+" ms (timing only; repeated warnings limited to once per minute).");}}}
 string WebsiteServerName(){
  if(!string.IsNullOrWhiteSpace(serverName.Value))return serverName.Value.Trim();
  try{var configured=AccessTools.Field(typeof(ZNet),"m_ServerName")?.GetValue(null) as string;if(!string.IsNullOrWhiteSpace(configured))return configured.Trim();}
  catch(Exception e){Warn("server display name",e);}
  return ZNet.instance.GetWorldName()??"Valheim Sagas";
 }
 void Tick() {
  if(activeWorld!=World||!ZNet.instance||ZNet.instance.GetWorldUID()==0){telemetryBudget.Reset(Time.unscaledTime);retryAfter.Clear();outboundProfile=null;}
  if(!ZNet.instance || ZNet.instance.GetWorldUID()==0) { RuntimeTerrain.Clear(); outbox?.Finish(pending.Values.ToArray());outbox=null; StopService(); activeWorld="";registered.Clear();online.Clear();pending.Clear();mediaTransfer.Clear();mediaCursors.Clear();return; }
  using var startupTrace=new StartupTrace(activeWorld!=World||startupTraceTicks<2,"world",message=>Logger.LogInfo(message));
  if(activeWorld!=World) {startupTraceTicks=0;profileTraceRecorded=false;outbox?.Finish(pending.Values.ToArray());StopService();service=null;activeWorld=World;nextSlsRefresh=0;mediaTransfer.Clear();mediaCursors.Clear();knownCharacters.Clear();registered.Clear();online.Clear();pending.Clear();sentCells.Clear();tileVersions.Clear();fullyMapped.Clear();RuntimeTerrain.Clear();pendingMedia.Clear();sentMedia.Clear();artwork?.Clear();mapCursor=0;importing=true;pendingMaps.Clear();journalId="";outbox=null;}
  startupTraceTicks++;startupTrace.Mark("world reset");
  if(ZNet.instance.IsServer() && service==null && Time.unscaledTime>=nextServiceStart) {
   try {
    if(serviceStartup.Poll()){var ready=serviceStartup.Current!;service=ready.Service;foreach(var id in ready.Characters)knownCharacters.Add(id);Logger.LogInfo("Sagas background service startup completed in "+ready.Milliseconds.ToString("F1",CultureInfo.InvariantCulture)+" ms (worker time).");}
    else if(!serviceStartup.Busy){
     // Snapshot all Unity/configuration values before entering the worker.
     var options=new SagaOptions{DataDirectory=data.Value,WebDirectory=Path.Combine(Path.GetDirectoryName(Info.Location)!,"web"),ListenPrefix=host.Value?prefix.Value:"",ViewerToken=token.Value,RequireViewerToken=requireToken.Value,World=World,WorldName=ZNet.instance.GetWorldName(),ServerName=WebsiteServerName(),ServerAddress=serverAddress.Value,SlsInstalled=SlsAdapter.Installed,LoreEnabled=lore.Value,LoreModel=model.Value,LoreAllowPaid=allowPaidLore.Value,LoreUseAccountPricing=true,LoreDailyBudget=Math.Max(0,daily.Value),LoreCooldownMinutes=Math.Max(1,cooldown.Value),LoreMilestoneEvents=Math.Max(1,milestones.Value),RetentionDays=Math.Max(0,retention.Value),StatisticsRetentionDays=statisticsRetention.Value<=0?0:Math.Max(retention.Value,statisticsRetention.Value),Log=message=>Logger.LogWarning(message),OpenRouterKey=Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")??key.Value};
     serviceStartup.Begin(()=>{var timer=System.Diagnostics.Stopwatch.StartNew();var created=new SagaService(options);try{created.Start();return new StartedService{Service=created,Characters=created.Store.Players(options.World).Select(p=>p.PlayerId).ToArray(),Milliseconds=timer.Elapsed.TotalMilliseconds};}catch{created.Dispose();throw;}});
    }
   }catch(Exception e){nextServiceStart=Time.unscaledTime+60;Warn("background service startup",e);}
  }
  startupTrace.Mark("service scheduling/adoption");

  foreach(var peer in ZNet.instance.GetPeers()) if(peer.IsReady() && registered.Add(peer.m_rpc)) {
   var captured=peer;
   RegisterLogin(peer);
   peer.m_rpc.Register<string>(RpcName,(rpc,json)=>Receive(captured,rpc,json));
   peer.m_rpc.Register<string>(AckName,(rpc,id)=>{if(!ZNet.instance.IsServer() && ZNet.instance.GetServerPeer()?.m_rpc==rpc) {pending.Remove(id);pendingMaps.Remove(id);pendingMedia.Remove(id);pendingPinPackets.Remove(id);}});
  }
  startupTrace.Mark("peer registration");
  if(service!=null) {
   var current=new HashSet<long>();
   foreach(var peer in ZNet.instance.GetPeers().Where(p=>p.IsReady() && p.m_playerID!=0)) {
    current.Add(peer.m_uid); var id=Identity(peer.m_playerID);knownCharacters.Add(id);

    service.TouchPresence(World,id,peer.m_playerName,peer.m_publicRefPos);
    online[peer.m_uid]=id;
   }
   foreach(var old in online.Keys.Where(id=>!current.Contains(id)).ToArray()){service.SetOffline(World,online[old]);online.Remove(old);}
  }
  startupTrace.Mark("presence");
  if(service!=null&&EnvMan.instance)service.UpdateClock(WorldClock.Sample(World,EnvMan.instance.GetDay(),EnvMan.instance.GetDayFraction(),ZNet.instance.GetTimeSeconds(),EnvMan.instance.m_dayLengthSec,Time.timeScale,ZNet.instance.GetNrOfPlayers()>0,EnvMan.instance.IsTimeSkipping()));
  var player=Player.m_localPlayer;
  var nextJournal=player&&player.GetPlayerID()!=0?(ZNet.instance.IsServer()?"host-":"")+Identity(player.GetPlayerID()):ZNet.instance.IsServer()?"server":"";
  if(nextJournal!=""&&journalId!=nextJournal){outbox?.Finish(pending.Values.ToArray());if(journalId!="")pending.Clear();ResetMapDetails();sentCells.Clear();tileVersions.Clear();fullyMapped.Clear();RuntimeTerrain.Clear();pendingMedia.Clear();sentMedia.Clear();artwork?.Clear();pendingMaps.Clear();mapCursor=0;importing=true;journalId=nextJournal;RunStage("outbox startup",()=>{outbox=new Outbox(data.Value,World+"-"+journalId,message=>Logger.LogWarning(message));foreach(var e in outbox.Load().Where(e=>e.World==World&&SagaService.ValidEvent(e)).Take(4096))pending[e.Id]=e;});}
  startupTrace.Mark("outbox initialization");

  if(player && player.GetPlayerID()!=0) {
   // Local hosts have no peer entry. Presence must not depend on equipment adapters.
   lastLocalId=Identity(player.GetPlayerID());knownCharacters.Add(lastLocalId);
   var publicPosition=ZNet.instance.IsReferencePositionPublic();
   var positionKey=lastLocalId+":"+sharePosition.Value+":"+publicPosition;
   if(positionLogKey!=positionKey){positionLogKey=positionKey;Logger.LogInfo("Website position sharing: "+(!sharePosition.Value?"hidden by Sagas 'Allow website position sharing' permission.":!publicPosition?"hidden; enable 'Visible to other players' on Valheim's map to show your marker.":"enabled by Valheim's map visibility setting.")+" Coordinates are not written to this log.");}
   RunStage("local presence",()=>service?.TouchPresence(World,lastLocalId,player.GetPlayerName(),ZNet.instance.IsReferencePositionPublic()));
   RunStage("equipment snapshot",()=>outboundProfile=new Packet{Player=Snapshot(player)});
   RunStage("exploration scan",()=>SyncMap(player));
  }
  startupTrace.Mark("player snapshot and exploration");
  foreach(var key in retryAfter.Where(x=>x.Value<Time.unscaledTime-60).Select(x=>x.Key).ToArray())retryAfter.Remove(key);
  RunStage("outbox save",()=>outbox?.Save(pending.Values.ToArray()));startupTrace.Mark("outbox save");
 }
 bool Send(Packet packet) {
  if(!ZNet.instance||World!=activeWorld||ZNet.instance.GetWorldUID()==0)return false;
  if(packet.Player!=null&&(packet.Player.ShareProfile!=shareProfile.Value||packet.Player.ShareMap!=shareMap.Value||packet.Player.SharePins!=sharePins.Value))return false;
  var retryKey=packet.Event?.Id??packet.AckId;
  if(retryKey!=""&&retryAfter.TryGetValue(retryKey,out var due)&&Time.unscaledTime<due)return false;
  if(ZNet.instance.IsServer()){if(service==null)return false;Apply(packet,0,null);if(retryKey!="")retryAfter[retryKey]=Time.unscaledTime+30;return true;}
  var peer=ZNet.instance.GetServerPeer(); if(peer==null||!peer.IsReady())return false;
  int queued=peer.m_socket.GetSendQueueSize();
  if(queued>=TelemetryBudget.QueueThreshold){if(!nextWarning.TryGetValue("network pressure",out var next)||Time.unscaledTime>=next){nextWarning["network pressure"]=Time.unscaledTime+60;Logger.LogWarning("Sagas uploads paused: game connection has "+queued+" queued bytes. Gameplay traffic takes priority; telemetry remains pending.");}return false;}
  int lane=packet.Exploration!=null||packet.Pins!=null?TelemetryBudget.Map:packet.Media!=null||packet.MediaChunk!=null?TelemetryBudget.Artwork:packet.Player!=null?TelemetryBudget.Profile:TelemetryBudget.Events;
  var json=packet.Wire??(packet.Wire=JsonConvert.SerializeObject(packet));int bytes=Encoding.UTF8.GetByteCount(json)+128;
  if(bytes>TelemetryBudget.MaximumPacketBytes){Warn("packet size",new InvalidOperationException("Sagas packet exceeded the bounded RPC size; upload not sent."));return false;}
  if(!telemetryBudget.TrySpend(lane,bytes,Time.unscaledTime,queued))return false;
  peer.m_rpc.Invoke(RpcName,json);if(retryKey!="")retryAfter[retryKey]=Time.unscaledTime+30;return true;
 }
 void PumpUploads(){
  if(!ZNet.instance||World!=activeWorld||ZNet.instance.GetWorldUID()==0)return;
  // Round-robin lanes, at most one RPC every250ms across all Sagas data.
  for(int i=0;i<4;i++)switch((uploadLane+i)%4){
   case 0:if(outboundProfile!=null&&Send(outboundProfile))outboundProfile=null;break;
   case 1:var events=pending.Values.ToArray();if(events.Length>0){for(int n=0;n<Math.Min(8,events.Length);n++)if(Send(new Packet{Event=events[(retryCursor+n)%events.Length]}))break;retryCursor=(retryCursor+8)%events.Length;}break;
   case 2:if(shareMap.Value){if(!SendMapDetails())foreach(var batch in pendingMaps.Values)if(Send(batch))break;}else if(pendingMaps.Count>0){pendingMaps.Clear();tileVersions.Clear();fullyMapped.Clear();sentCells.Clear();}break;
   case 3:if(shareProfile.Value)SendArtwork();else{pendingMedia.Clear();mediaCursors.Clear();}break;
  }
  uploadLane=(uploadLane+1)%4;
 }
 void SendArtwork() {
  mediaTransfer.Expire(Time.unscaledTime);
  foreach(var old in mediaCursors.Keys.Where(x=>!pendingMedia.ContainsKey(x)).ToArray())mediaCursors.Remove(old);
  int budget=4;
  // Icons remain small legacy packets. Prioritize them so a portrait cannot hold up equipment imagery.
  foreach(var packet in pendingMedia.Values.OrderBy(x=>x.Media?.Kind=="portrait"?1:0).ToArray()) {
   if(budget<=0)break;
   var media=packet.Media;if(media==null)continue;
   if(ZNet.instance.IsServer()||!MediaTransfer.NeedsChunks(media)){if(Send(packet))budget--;continue;}
   int cursor=mediaCursors.TryGetValue(packet.AckId,out var index)?index:0;
   int count=MediaTransfer.ChunkCount(media.Png.Length),send=Math.Min(budget,count);
   for(int i=0;i<send;i++){
    // Do not allocate/base64-encode a64KiB chunk until its byte allowance is available.
    var peer=ZNet.instance.GetServerPeer();if(peer==null)break;
    int length=Math.Min(MediaTransfer.ChunkBytes,media.Png.Length-cursor*MediaTransfer.ChunkBytes);
    if(!telemetryBudget.Ready(TelemetryBudget.Artwork,((length+2)/3)*4+1024,Time.unscaledTime,peer.m_socket.GetSendQueueSize()))break;
    if(retryAfter.TryGetValue(packet.AckId,out var due)&&Time.unscaledTime<due)break;
    if(!Send(new Packet{Version=2,MediaChunk=MediaTransfer.Slice(media,cursor)}))break;
    cursor=(cursor+1)%count;budget--;
    if(cursor==0){retryAfter[packet.AckId]=Time.unscaledTime+30;break;}
   }
   mediaCursors[packet.AckId]=cursor;
  }
 }
 void Receive(ZNetPeer peer,ZRpc rpc,string json) {
  if(service==null || !ZNet.instance.IsServer() || peer.m_rpc!=rpc || !peer.IsReady() || peer.m_playerID==0 || json.Length>128000)return;
  var now=Time.unscaledTime; if(!rates.TryGetValue(rpc,out var rate)||now-rate.start>10)rate=(now,0); rate.count++; rates[rpc]=rate;if(rate.count>160)return;
  try { var packet=JsonConvert.DeserializeObject<Packet>(json,new JsonSerializerSettings{MaxDepth=12,TypeNameHandling=TypeNameHandling.None});if(packet!=null && (packet.Version==1&&packet.MediaChunk==null||packet.Version==2&&packet.MediaChunk!=null&&packet.Media==null&&packet.Event==null&&packet.Player==null&&packet.Exploration==null&&packet.Pins==null)){if(packet.Event!=null&&(packet.Event.Kind=="collect"||packet.Event.Kind=="pickup"||packet.Event.Kind=="death"||packet.Event.Kind=="bounty"))packet.Event.PlayerName=peer.m_playerName;if(packet.Player!=null){packet.Player.Name=peer.m_playerName;packet.Player.SharePosition &= peer.m_publicRefPos;} Apply(packet,peer.m_playerID,rpc);} } catch(Exception e){Logger.LogDebug("Rejected Sagas packet: "+e.Message);}
 }
 void Apply(Packet packet,long peerPlayer,ZRpc? rpc) {
  if(service==null)return;
  if(packet.Pins!=null&&packet.Pins.World!=World||packet.MediaChunk!=null&&packet.MediaChunk.World!=World||packet.Media!=null&&packet.Media.World!=World||packet.Player!=null&&packet.Player.World!=World||packet.Exploration!=null&&packet.Exploration.World!=World||packet.Event!=null&&packet.Event.World!=World)return;
  if(packet.Event!=null&&(packet.Event.Kind=="join"||packet.Event.Kind=="leave"))return;
  var id=peerPlayer==0 ? Identity(Player.m_localPlayer?Player.m_localPlayer.GetPlayerID():0) : Identity(peerPlayer);
  if(packet.MediaChunk!=null){var assembled=mediaTransfer.Accept(id,World,packet.MediaChunk,Time.unscaledTime);if(assembled!=null){packet.Media=assembled;packet.AckId="media:"+assembled.Id;}}
  if(packet.Media!=null){var m=packet.Media;m.World=World;m.PlayerId=id;service.UploadMedia(m,ok=>{if(ok)committed.Enqueue(()=>{if(rpc!=null)rpc.Invoke(AckName,packet.AckId);else {pendingMedia.Remove(packet.AckId);pendingPinPackets.Remove(packet.AckId);}});});}
  if(packet.Player!=null) {var p=packet.Player;p.World=World;p.PlayerId=id;p.Utc=DateTime.UtcNow;p.Online=true;p.NemesisScore=HostNemesisScore(peerPlayer==0&&Player.m_localPlayer?Player.m_localPlayer.GetPlayerID():peerPlayer); if(!service.UpdatePlayer(p))Warn("equipment validation",new InvalidOperationException("Snapshot rejected or storage queue full; presence and exploration continue."));}
  if(packet.Pins!=null){packet.Pins.World=World;packet.Pins.PlayerId=id;service.UpdatePins(packet.Pins,ok=>{if(ok)committed.Enqueue(()=>{if(rpc!=null)rpc.Invoke(AckName,packet.AckId);else pendingPinPackets.Remove(packet.AckId);});});}
  if(packet.Exploration!=null) {var e=packet.Exploration;e.World=World;e.PlayerId=id;if(e.CellSize!=64||e.Cells.Count>128)return;service.Explore(e,ok=>{if(ok)committed.Enqueue(()=>{if(rpc!=null)rpc.Invoke(AckName,packet.AckId);else pendingMaps.Remove(packet.AckId);});});}
  if(packet.Event!=null) {var e=packet.Event;e.World=World;e.NemesisBoss &= SlsAdapter.Installed&&e.Kind=="kill"; if(e.Kind=="collect"||e.Kind=="pickup"||e.Kind=="death"||e.Kind=="bounty"){if(e.PlayerId!=id){if(rpc!=null)rpc.Invoke(AckName,e.Id);else pending.Remove(e.Id);return;}e.PlayerId=id;}
   if(e.Kind=="kill"||e.Kind=="drop"){foreach(var peer in ZNet.instance.GetPeers())if(peer.IsReady()&&peer.m_playerID!=0)knownCharacters.Add(Identity(peer.m_playerID));if(!knownCharacters.Contains(e.PlayerId)){e.PlayerId="";e.PlayerName="Unattributed";}e.Contributors=e.Contributors.Where(knownCharacters.Contains).ToList();}
   if(e.Id.Length>160||e.Amount<1||e.Amount>10000||e.Stars<0||e.Stars>1000)return;
   service.TryEvent(e,ok=>{if(ok)committed.Enqueue(()=>{if(rpc!=null)rpc.Invoke(AckName,e.Id);else pending.Remove(e.Id);});});
  }
 }
 internal void Record(SagaEvent e) { e.World=World;if(e.World==""||e.Id==""||!SagaService.ValidEvent(e))return;if(pending.Count<4096){if(!pending.ContainsKey(e.Id))SagasNotifications.Notify(e,notifications.Value,Identity(Player.m_localPlayer?Player.m_localPlayer.GetPlayerID():0));pending[e.Id]=e;}else Logger.LogWarning("Sagas outbox full (4096 events): new event not retained; check server connection/storage."); }
 string QueueArt(RuntimeArt.Image? image){if(image==null)return "";if(image.Kind=="portrait")foreach(var old in pendingMedia.Where(x=>x.Value.Media?.Kind=="portrait"&&x.Value.Media.Id!=image.Id).Select(x=>x.Key).ToArray()){sentMedia.Remove(pendingMedia[old].Media!.Id);pendingMedia.Remove(old);}if((!sentMedia.TryGetValue(image.Id,out var lastSent)||Time.unscaledTime-lastSent>120)&&pendingMedia.Count<64){var packet=new Packet{AckId="media:"+image.Id,Media=new MediaUpload{World=World,PlayerId=lastLocalId,Id=image.Id,Kind=image.Kind,Png=image.Png}};pendingMedia[packet.AckId]=packet;if(sentMedia.Count>=256)sentMedia.Remove(sentMedia.OrderBy(x=>x.Value).First().Key);sentMedia[image.Id]=Time.unscaledTime;}return image.Id;}
 PlayerSnapshot Snapshot(Player p) {
  using var timing=new StartupTrace(!profileTraceRecorded,"equipment",message=>Logger.LogInfo(message));profileTraceRecorded=true;
  if(!shareProfile.Value){pendingMedia.Clear();sentMedia.Clear();artwork?.Clear();}lastLocalId=Identity(p.GetPlayerID());knownCharacters.Add(lastLocalId);var pos=p.transform.position;var s=new PlayerSnapshot{World=World,PlayerId=Identity(p.GetPlayerID()),Name=p.GetPlayerName(),Online=true,ShareProfile=shareProfile.Value,ShareMap=shareMap.Value,SharePins=sharePins.Value,SharePosition=sharePosition.Value&&ZNet.instance.IsReferencePositionPublic(),X=pos.x,Z=pos.z};
  timing.Mark("identity and permissions");
  foreach(var item in p.GetInventory().GetEquippedItems()){var gear=Gear.Read(item);EquippedState.Apply(gear,item,p);if(shareProfile.Value)gear.IconId=QueueArt(artwork?.TryIcon(item));s.Gear.Add(gear);} timing.Mark("equipped metadata");if(shareProfile.Value){RunStage("portrait capture",()=>s.PortraitId=QueueArt(artwork?.TryPortrait(p,!pendingMedia.Values.Any(x=>x.Media?.Kind=="portrait"))));s.PortraitStatus=artwork?.Status??"waiting-for-player";}timing.Mark("portrait scheduling");s.Hotbar=shareProfile.Value?HotbarCapture.Read(p,item=>QueueArt(artwork?.TryIcon(item))):new List<GearItem>();timing.Mark("hotbar metadata");s.EffectiveResistances=RuntimeArt.EffectiveResistances(p);timing.Mark("resistances");
  s.EffectiveStats["Armor"]=p.GetBodyArmor();s.EffectiveStats["Health"]=p.GetHealth();s.EffectiveStats["Max health"]=p.GetMaxHealth();s.EffectiveStats["Max stamina"]=p.GetMaxStamina();s.EffectiveStats["Max eitr"]=p.GetMaxEitr();
  if(shareProfile.Value){s.EpicLootInstalled=EpicProgress.Installed;s.Gold=EpicProgress.CarriedGold(p);}
  timing.Mark("effective stats and gold");return s;
 }
 void SyncMap(Player p) {
  if(!shareMap.Value||!Minimap.instance||WorldGenerator.instance==null||pendingMaps.Count>=2||RuntimeTerrainShader.Pending)return;
  var map=Minimap.instance;var bits=exploredField.GetValue(map) as BitArray;if(bits==null)return;
  // Every exported sample is personally known; shader capture never exports cartography-only terrain.
  var batch=new ExplorationBatch{World=World,PlayerId=Identity(p.GetPlayerID()),Imported=importing};
  const int width=ExplorationScan.Width;var budget=1600;var payloadBytes=0;bool full=false;
  // Show nearby explored terrain immediately; continue bounded whole-world import below.
  var pos=p.transform.position;
  foreach(var cell in ExplorationScan.Nearby(pos.x,pos.z)){AddCell(cell.X,cell.Z);if(batch.Cells.Count>=64||full)break;}
  while(budget-->0 && batch.Cells.Count<64&&!full) {
   var cx=mapCursor%width-width/2;var cz=mapCursor/width-width/2;mapCursor++;if(mapCursor>=width*width){mapCursor=0;importing=false;}
   AddCell(cx,cz);
  }
  void AddCell(int cx,int cz) {
   if(payloadBytes>74000){full=true;return;}var keyCell=cx+":"+cz;if(fullyMapped.Contains(keyCell))return;var pixels=RuntimeTerrain.Capture(map,bits,cx,cz);if(pixels==""){if(RuntimeTerrainShader.Pending)full=true;return;}var rgba=Convert.FromBase64String(pixels);using var tileHash=SHA256.Create();var signature=Convert.ToBase64String(tileHash.ComputeHash(rgba));if(tileVersions.TryGetValue(keyCell,out var previous)&&previous==signature)return;payloadBytes+=pixels.Length;if(payloadBytes>74000)full=true;tileVersions[keyCell]=signature;if(Enumerable.Range(0,rgba.Length/4).All(i=>rgba[i*4+3]==255))fullyMapped.Add(keyCell);
   var wx=(cx+.5f)*64;var wz=(cz+.5f)*64;
   batch.Cells.Add(new MapCell{X=cx,Z=cz,Biome="Explored terrain",Height=0,TerrainPixels=pixels});sentCells.Add(keyCell);full=true; // One terrain tile per paced packet.
  }
  if(batch.Cells.Count>0){var packet=new Packet{Exploration=batch,AckId="map:"+Guid.NewGuid().ToString("N")};pendingMaps[packet.AckId]=packet;}
 }
 public sealed class Packet {[JsonIgnore]internal string? Wire;public int Version=1;public string AckId="";public SagaEvent? Event;public PlayerSnapshot? Player;public ExplorationBatch? Exploration;public MapPins? Pins;public MediaUpload? Media;public MediaChunk? MediaChunk;}
}
