using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
namespace ValheimSagas;
public sealed partial class SagaService : IDisposable {
 readonly SagaOptions options; readonly SagaStore store; readonly BlockingCollection<Action> queue; readonly CancellationTokenSource stop=new CancellationTokenSource();
 readonly SagaDiagnostics diagnostics; string? errorId;
 readonly JsonSerializerSettings json=new JsonSerializerSettings{ContractResolver=new CamelCasePropertyNamesContractResolver(),DateTimeZoneHandling=DateTimeZoneHandling.Utc};
 readonly SemaphoreSlim httpSlots=new SemaphoreSlim(8); Task? writer,web,lore; HttpListener? listener; bool started,disposed; long rejected; string? failure; DateTime nextRetention=DateTime.MinValue; int gzipDisabled;
 public long Rejected=>Interlocked.Read(ref rejected);
 public SagaStore Store=>store;
 readonly System.Net.Http.HttpClient? loreClient;
 public SagaService(SagaOptions options,System.Net.Http.HttpClient? loreClient=null){this.loreClient=loreClient;this.options=options;diagnostics=new SagaDiagnostics(options);store=new SagaStore(options.DataDirectory);if(TextValid(options.World,100,true)&&TextValid(options.WorldName,200,true))store.WorldName(options.World,options.WorldName);queue=new BlockingCollection<Action>(Math.Max(64,options.QueueCapacity));}
 public void Start(){if(started)return;if(!string.IsNullOrWhiteSpace(options.ListenPrefix)&&options.RequireViewerToken&&options.ViewerToken.Length<24)throw new ArgumentException("ViewerToken must contain at least 24 characters.");started=true;writer=Task.Run(()=>{foreach(var action in queue.GetConsumingEnumerable()){try{action();}catch(Exception e){failure=e.GetType().Name;errorId=diagnostics.Report("storage",e);}}});
  foreach(var world in store.Worlds().Concat(new[]{options.World}).Distinct())store.QueueServerLore(world);
  lore=Task.Run(LoreLoop);
  if(!string.IsNullOrWhiteSpace(options.ListenPrefix)) {if(options.RequireViewerToken&&options.ViewerToken.Length<24)throw new ArgumentException("ViewerToken must contain at least 24 characters.");listener=new HttpListener();listener.Prefixes.Add(options.ListenPrefix);try{listener.Start();web=Task.Run(WebLoop);}catch(Exception e){errorId=diagnostics.Report("website-listener",e);listener.Close();listener=null;}}
 }
 bool Enqueue(Action a){if(disposed||queue.IsAddingCompleted)return false;try{if(queue.TryAdd(a))return true;}catch(InvalidOperationException){}Interlocked.Increment(ref rejected);return false;}
 static T Copy<T>(T value)=>JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value))!;
 static bool TextValid(string? x,int max,bool required=false)=>x!=null&&x.Length<=max&&(!required||x.Length>0)&&!x.Any(char.IsControl);
 static bool Coordinate(float? x)=>!x.HasValue||(!float.IsNaN(x.Value)&&!float.IsInfinity(x.Value)&&Math.Abs(x.Value)<=20000);
 static bool StatsValid(Dictionary<string,float>? values)=>values!=null&&values.Count<=128&&values.All(p=>TextValid(p.Key,160)&&!float.IsNaN(p.Value)&&!float.IsInfinity(p.Value));
 static bool GearValid(GearItem? g)=>g!=null&&g.HotbarSlot>=0&&g.HotbarSlot<=8&&MediaId(g.IconId)&&TextValid(g.Name,200)&&TextValid(g.Prefab,200)&&TextValid(g.Type,100)&&TextValid(g.Slot,100)&&TextValid(g.Rarity,80)&&RarityColors.Valid(g.RarityColor)&&TextValid(g.Note,2000)&&g.Quality>=1&&g.Quality<=1000&&StatsValid(g.BaseStats)&&StatsValid(g.Stats)&&g.Effects!=null&&g.Effects.Count<=64&&g.Effects.All(e=>TextValid(e,500))&&!float.IsNaN(g.Durability)&&!float.IsInfinity(g.Durability)&&!float.IsNaN(g.MaxDurability)&&!float.IsInfinity(g.MaxDurability);
 public static bool ValidEvent(SagaEvent e)=>TextValid(e.Id,240,true)&&TextValid(e.World,100,true)&&TextValid(e.PlayerId,100)&&TextValid(e.PlayerName,100)&&TextValid(e.Name,200)&&TextValid(e.Prefab,200)&&TextValid(e.Source,100)&&TextValid(e.Provenance,240)&&TextValid(e.Rarity,80)&&RarityColors.Valid(e.RarityColor)&&TextValid(e.ItemType,100)&&TextValid(e.Biome,100)&&e.Effects!=null&&e.Effects.Count<=64&&e.Effects.All(x=>TextValid(x,500))&&e.Contributors!=null&&e.Contributors.Count<=64&&e.Contributors.All(x=>TextValid(x,100))&&new[]{"kill","drop","collect","pickup","death","join","leave","bounty"}.Contains(e.Kind)&&(e.Kind!="bounty"||e.Amount==1)&&(!e.DurationSeconds.HasValue||(!double.IsNaN(e.DurationSeconds.Value)&&!double.IsInfinity(e.DurationSeconds.Value)&&e.DurationSeconds.Value>0&&e.DurationSeconds.Value<=604800))&&e.Stars>=0&&e.Stars<=1000&&e.Amount>0&&e.Amount<=1000000&&e.Quality>=1&&e.Quality<=1000&&Coordinate(e.X)&&Coordinate(e.Z)&&e.Utc.Kind==DateTimeKind.Utc&&e.Utc<=DateTime.UtcNow.AddMinutes(2)&&e.Utc>=new DateTime(2020,1,1,0,0,0,DateTimeKind.Utc);
 public bool TryEvent(SagaEvent e)=>TryEvent(e,null);
 public bool TryEvent(SagaEvent e,Action<bool>? committed){if(!ValidEvent(e)||(e.Kind=="collect"&&string.IsNullOrEmpty(e.Provenance))){Interlocked.Increment(ref rejected);return false;}var copy=Copy(e);return Enqueue(()=>{try{store.AddEvent(copy);if(!string.IsNullOrEmpty(copy.PlayerId)&&(copy.Kind=="kill"||copy.Kind=="collect"||copy.Kind=="death"||copy.Kind=="bounty"||copy.Kind=="leave"))store.QueueLore(copy.World,copy.PlayerId);committed?.Invoke(true);}catch{committed?.Invoke(false);throw;}});}
 public bool UpdatePlayer(PlayerSnapshot p){if(p.NemesisScore.HasValue&&!Finite(p.NemesisScore.Value))p.NemesisScore=null;if(p.Gold<0||!TextValid(p.PortraitStatus,300)||p.Hotbar==null||p.Hotbar.Count>8||p.Hotbar.Any(g=>!GearValid(g)||g.HotbarSlot<1)||p.Hotbar.Select(g=>g.HotbarSlot).Distinct().Count()!=p.Hotbar.Count||!MediaId(p.PortraitId)||p.EffectiveResistances==null||p.EffectiveResistances.Count>32||p.EffectiveResistances.Any(r=>!TextValid(r.Key,80)||!TextValid(r.Value,100))||!TextValid(p.World,100,true)||!TextValid(p.PlayerId,100,true)||!TextValid(p.Name,100)||p.Gear==null||p.Gear.Count>32||p.Gear.Any(g=>!GearValid(g))||!StatsValid(p.EffectiveStats)||!TextValid(p.StatsNote,2000)||!Coordinate(p.X)||!Coordinate(p.Z)||JsonConvert.SerializeObject(p).Length>65536)return false;var copy=Copy(p);copy.ProfileBiome="";copy.ProfileBiomeEvidence="";copy.Utc=DateTime.UtcNow;copy.LastSeenUtc=copy.Utc;copy.PositionUtc=copy.SharePosition&&copy.X.HasValue&&copy.Z.HasValue?copy.Utc:(DateTime?)null;copy.PositionLive=false;if(!copy.SharePosition){copy.X=null;copy.Z=null;}return Enqueue(()=>{var previous=store.Players(copy.World).FirstOrDefault(x=>x.PlayerId==copy.PlayerId);if(copy.ShareProfile&&copy.PortraitId!=""&&previous!=null&&previous.ShareProfile&&previous.PortraitId!=""&&copy.PortraitId!=previous.PortraitId&&!store.HasMedia(copy.World,copy.PlayerId,copy.PortraitId))copy.PortraitId=previous.PortraitId;store.Player(copy);if(copy.Online&&(previous==null||!previous.Online))RecordTransition(copy,"join");});}
 public bool Explore(ExplorationBatch b)=>Explore(b,null);
 public bool Explore(ExplorationBatch b,Action<bool>? committed){if(!TextValid(b.World,100,true)||!TextValid(b.PlayerId,100,true)||b.CellSize!=64||b.Cells==null||b.Cells.Count>1024||b.Cells.Sum(c=>(long)(c.TerrainPixels?.Length??0))>96000||b.Cells.Any(c=>Math.Abs((long)c.X)>313||Math.Abs((long)c.Z)>313||!TextValid(c.Biome,80)||!TerrainTile.Valid(c.TerrainPixels)||float.IsNaN(c.Height)||float.IsInfinity(c.Height)))return false;var copy=Copy(b);return Enqueue(()=>{try{store.Explore(copy);committed?.Invoke(true);}catch{committed?.Invoke(false);throw;}});}
 public bool TouchPresence(string world,string playerId,string name,bool? publicPosition=null){if(!TextValid(world,100,true)||!TextValid(playerId,100,true)||!TextValid(name,100))return false;return Enqueue(()=>{var p=store.Players(world).FirstOrDefault(x=>x.PlayerId==playerId);bool joined=p==null||!p.Online;p=p??new PlayerSnapshot{World=world,PlayerId=playerId,Utc=DateTime.MinValue,StatsNote="No equipment snapshot received."};p.Name=name;p.Online=true;if(publicPosition==false){p.SharePosition=false;p.X=null;p.Z=null;p.PositionUtc=null;p.PositionLive=false;}p.LastSeenUtc=DateTime.UtcNow;store.Player(p);if(joined)RecordTransition(p,"join");});}
 public void SetOffline(string world,string playerId){Enqueue(()=>{var p=store.Players(world).FirstOrDefault(x=>x.PlayerId==playerId);if(p==null||!p.Online)return;p.Online=false;p.LastSeenUtc=DateTime.UtcNow;store.Player(p);RecordTransition(p,"leave");store.QueueLore(world,playerId);});}
 void RecordTransition(PlayerSnapshot p,string kind){store.AddEvent(new SagaEvent{Id="session-"+Guid.NewGuid().ToString("N"),World=p.World,PlayerId=p.PlayerId,PlayerName=p.Name,Kind=kind,Utc=p.LastSeenUtc??DateTime.UtcNow,Source="server-connection"});}
 public bool Flush(int milliseconds=10000){var signal=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);if(!Enqueue(()=>signal.TrySetResult(true)))return false;return signal.Task.Wait(milliseconds);}
 public object State(string world,TimeWindow window,HashSet<string>? selected=null) {
  var sls=ActiveSls(world);var players=store.Players(world);foreach(var p in players)if(!p.ShareProfile||!sls.Installed||!sls.NemesisEnabled)p.NemesisScore=null;var shared=new HashSet<string>(players.Where(p=>p.ShareMap).Select(p=>p.PlayerId));var visible=new HashSet<string>(players.Where(p=>p.ShareProfile).Select(p=>p.PlayerId));visible.Add("");
  var cells=store.Cells(world,shared);var known=cells.ToDictionary(c=>c.X+":"+c.Z);
  // One complete history query serves backdrop progression and filtered analytics together.
  var history=store.Events(world,new TimeWindow(DateTime.MinValue,DateTime.MaxValue));
  ApplyProfileBiomes(players,history);
  var events=history.Where(e=>window.Contains(e.Utc)&&(selected==null||selected.Contains(e.PlayerId))&&visible.Contains(e.PlayerId)).ToList();
  foreach(var e in events)if(!shared.Contains(e.PlayerId)||!e.X.HasValue||!e.Z.HasValue||(!known.TryGetValue((int)Math.Floor(e.X.Value/64)+":"+(int)Math.Floor(e.Z.Value/64),out var tile)||!TerrainTile.Known(tile,e.X.Value,e.Z.Value))){e.X=null;e.Z=null;}
  foreach(var p in players){p.Online=p.Online&&(DateTime.UtcNow-(p.LastSeenUtc??p.Utc)).TotalSeconds<=options.PresenceTimeoutSeconds;if(!p.SharePosition||!p.X.HasValue||!p.Z.HasValue){p.X=null;p.Z=null;p.PositionUtc=null;p.PositionLive=false;}else{p.PositionUtc=p.PositionUtc??p.Utc;p.PositionLive=p.Online&&(DateTime.UtcNow-p.PositionUtc.Value).TotalSeconds<=options.PresenceTimeoutSeconds;}if(!p.ShareProfile){p.Gear.Clear();p.Hotbar.Clear();p.PortraitStatus="";p.EffectiveStats.Clear();p.EffectiveResistances.Clear();p.PortraitId="";p.Gold=null;p.EpicLootInstalled=false;}}
  foreach(var e in events)e.Contributors=e.Contributors.Where(id=>!string.IsNullOrEmpty(id)&&visible.Contains(id)).Distinct(StringComparer.Ordinal).ToList();
  var kills=events.Where(e=>e.Kind=="kill").ToList();var loot=events.Where(e=>e.Kind=="drop"||e.Kind=="collect"||e.Kind=="pickup").ToList();
  var chapters=store.Chapters(world).Where(c=>GeneratedChapter(c)&&visible.Contains(c.PlayerId)&&ChapterContextVisible(c)).Select(WithoutTemplateBio).ToArray();
  return new {sls=SlsSummary(sls),server=new{name=options.ServerName,address=options.ServerAddress},world,worlds=store.Worlds(),worldNames=store.WorldNames(),trackingSince=store.TrackingSince,statisticsSince=store.StatisticsSince,detailRetentionDays=options.RetentionDays,partialHistory=window.From<store.TrackingSince||(store.StatisticsSince.HasValue&&window.From<store.StatisticsSince.Value),from=window.From,to=window.To,players,events=events.OrderByDescending(e=>e.Utc).Take(5000).ToArray(),eventCount=events.Count,eventsTruncated=events.Count>5000,chapters,synthetic=options.Synthetic,
   stats=new{nemesisBossKills=history.Where(e=>e.Kind=="kill"&&e.NemesisBoss&&window.Contains(e.Utc)).Count(e=>e.Contributors.Concat(new[]{e.PlayerId}).Any(id=>id!=""&&visible.Contains(id)&&(selected==null||selected.Contains(id)))),bounties=events.Count(e=>e.Kind=="bounty"),deaths=events.Count(e=>e.Kind=="death"),bossKills=kills.Count(e=>e.Boss),highestStars=kills.Count==0?(int?)null:kills.Max(e=>e.Stars),creatureTypes=kills.Select(e=>string.IsNullOrEmpty(e.Prefab)?e.Name:e.Prefab).Distinct(StringComparer.OrdinalIgnoreCase).Count(),rarityCollected=loot.Where(e=>e.Kind=="collect").GroupBy(e=>LeaderboardRarity(e.Rarity)).ToDictionary(g=>g.Key,g=>g.Sum(e=>(long)e.Amount)),kills=kills.Count,dropped=loot.Where(e=>e.Kind=="drop").Sum(e=>(long)e.Amount),collected=loot.Where(e=>e.Kind=="collect").Sum(e=>(long)e.Amount),unknownPickups=loot.Where(e=>e.Kind=="pickup").Sum(e=>(long)e.Amount),stars=kills.GroupBy(e=>e.Stars).ToDictionary(g=>g.Key.ToString(),g=>g.Count()),
    creatures=kills.GroupBy(e=>new{e.Prefab,e.Name,e.Stars,e.Biome,e.Boss}).Select(g=>new{g.Key.Prefab,g.Key.Name,g.Key.Stars,g.Key.Biome,g.Key.Boss,count=g.Count()}).OrderByDescending(x=>x.count).ToArray(),
    loot=loot.GroupBy(e=>new{e.Prefab,e.Name,e.Kind,e.ItemType,e.Quality,e.Rarity}).Select(g=>new{g.Key.Prefab,g.Key.Name,g.Key.Kind,g.Key.ItemType,g.Key.Quality,g.Key.Rarity,rarityColor=g.OrderByDescending(e=>e.Utc).Select(e=>e.RarityColor).FirstOrDefault(c=>!string.IsNullOrEmpty(c))??"",amount=g.Sum(e=>(long)e.Amount)}).OrderByDescending(x=>x.amount).ToArray()},
   health=Health(),coverage="Requires Sagas on all players; unknown provenance pickups excluded from earned loot. Fog-aware 2D terrain."};
 }
 public object Map(string world,HashSet<string>? selected=null){var ids=new HashSet<string>(store.Players(world).Where(p=>p.ShareMap&&(selected==null||selected.Contains(p.PlayerId))).Select(p=>p.PlayerId));var cells=store.Cells(world,ids);return new{sls=SlsMap(world,cells),cellSize=64,cells,imported=store.Imported(world,ids),label="Voluntarily shared known map; imported discoveries predate tracking. Runtime-rendered Valheim map with personal fog. Older or fallback tiles may have lower detail."};}
 static bool LoreEvent(SagaEvent e)=>e.Kind=="kill"||e.Kind=="collect"||e.Kind=="death"||e.Kind=="bounty";
 bool Milestone(IReadOnlyList<SagaEvent> events)=>events.Count>0&&(events.Count>=Math.Max(1,options.LoreMilestoneEvents)||events.Any(e=>e.Boss||e.Stars>=3||!string.IsNullOrEmpty(e.Rarity)));
 // Store reads deserialize independent snapshots; this projection never writes stored history.
 static SagaChapter WithoutTemplateBio(SagaChapter c){if(c.CharacterBioModel=="local-template")c.CharacterBio="";if(c.ServerBioModel=="local-template")c.ServerBio="";return c;}
 static bool GeneratedChapter(SagaChapter c)=>c.Model!="local-template"&&!string.IsNullOrWhiteSpace(c.Text);
 bool ChapterContextVisible(SagaChapter chapter){var players=store.Players(chapter.World);return chapter.Participants.All(p=>players.Any(x=>x.PlayerId==p.PlayerId&&x.ShareProfile))&&chapter.MapParticipants.All(id=>players.Any(p=>p.PlayerId==id&&p.ShareProfile&&p.ShareMap));}
 bool SharedChapterVisible(SagaChapter chapter,HashSet<string> consent)=>ChapterContextVisible(chapter)&&chapter.Scope=="server"&&chapter.Participants.Count>0&&chapter.Participants.All(p=>consent.Contains(p.PlayerId));
 public object ServerSaga(string world) {
  var consent=new HashSet<string>(store.Players(world).Where(p=>p.ShareProfile).Select(p=>p.PlayerId));
  var chapters=store.ServerChapters(world).Where(c=>GeneratedChapter(c)&&SharedChapterVisible(c,consent)).Select(WithoutTemplateBio).ToArray();
  var latest=chapters.LastOrDefault();
  var covered=new HashSet<string>(store.ServerChapters(world).Where(GeneratedChapter).SelectMany(c=>c.EventIds));
  var eligible=store.Events(world,new TimeWindow(DateTime.MinValue,DateTime.UtcNow),consent).Where(e=>LoreEvent(e)&&!covered.Contains(e.Id)).ToArray();
  var names=store.WorldNames();
  return new{server=new{name=options.ServerName,address=options.ServerAddress},world,worldName=names.TryGetValue(world,out var title)?title:(world==options.World?options.WorldName:""),trackingSince=store.TrackingSince,synthetic=options.Synthetic,
   bio=latest?.ServerBio??"",bioModel=latest?.ServerBioModel??"local-template",chapters,participants=chapters.SelectMany(c=>c.Participants).GroupBy(p=>p.PlayerId,StringComparer.Ordinal).Select(g=>g.Last()).OrderBy(p=>p.Name,StringComparer.Ordinal).ToArray(),pending=options.LoreEnabled&&!string.IsNullOrWhiteSpace(options.OpenRouterKey)&&store.PendingServerLore().Contains(world)&&Milestone(eligible),fictional=true};
 }
 async Task LoreLoop(){
  var engine=new LoreEngine(options,loreClient,()=>store.ReserveLoreRequest(DateTime.UtcNow,options.LoreDailyBudget));
  while(!stop.IsCancellationRequested){
   try{
    if(DateTime.UtcNow>=nextRetention){store.ApplyRetention(DateTime.UtcNow,options.RetentionDays,options.StatisticsRetentionDays);nextRetention=DateTime.UtcNow.AddMinutes(5);}
    if(!options.LoreEnabled||string.IsNullOrWhiteSpace(options.OpenRouterKey)){await Task.Delay(10000,stop.Token);continue;}
    foreach(var item in store.PendingLore()){
     if(stop.IsCancellationRequested)break;
     var p=store.Players(item.World).FirstOrDefault(x=>x.PlayerId==item.Player);if(p==null)continue;
     if(!p.ShareProfile){store.CompleteLore(item.World,item.Player);continue;}
     var chapters=store.Chapters(item.World).Where(c=>c.PlayerId==item.Player&&GeneratedChapter(c)).ToArray();var previous=chapters.LastOrDefault(ChapterContextVisible);
     if(previous!=null&&(DateTime.UtcNow-previous.Utc).TotalMinutes<options.LoreCooldownMinutes)continue;
     var covered=new HashSet<string>(chapters.SelectMany(c=>c.EventIds));
     var career=store.Events(item.World,new TimeWindow(DateTime.MinValue,DateTime.UtcNow),new HashSet<string>{item.Player}).Where(LoreEvent).OrderBy(e=>e.Utc).ThenBy(e=>e.Id,StringComparer.Ordinal).ToList();
     var ev=career.Where(e=>!covered.Contains(e.Id)).ToList();if(!Milestone(ev))continue;
     if(!store.LoreReady(item.World,item.Player))continue;
     var playerOptions=PlayerLoreOptions(item.World,item.Player);var playerEngine=new LoreEngine(playerOptions,loreClient,()=>ReferenceEquals(playerOptions,options)?store.ReserveLoreRequest(DateTime.UtcNow,options.LoreDailyBudget):store.ReservePersonalLore(item.World,item.Player,playerOptions.LoreDailyBudget));
     var chapter=await playerEngine.GenerateAsync(p.Name,ev.Take(100).ToArray(),previous,stop.Token,career,store.NarrativeContext(item.World,item.Player));if(GeneratedChapter(chapter)&&store.Players(item.World).Any(x=>x.PlayerId==item.Player&&x.ShareProfile)&&ChapterContextVisible(chapter))store.Chapter(chapter);else store.DeferLore(item.World,item.Player);
    }
    foreach(var world in store.PendingServerLore()){
     if(stop.IsCancellationRequested)break;
     var sharers=store.Players(world).Where(p=>p.ShareProfile).ToDictionary(p=>p.PlayerId,p=>p.Name,StringComparer.Ordinal);
     var consent=new HashSet<string>(sharers.Keys);var allChapters=store.ServerChapters(world).Where(GeneratedChapter).ToArray();
     // Hidden history still covers its event IDs. Revocation never silently rewrites chapters.
     var previous=allChapters.LastOrDefault(c=>SharedChapterVisible(c,consent));
     var mostRecent=allChapters.LastOrDefault();
     if(mostRecent!=null&&(DateTime.UtcNow-mostRecent.Utc).TotalMinutes<options.LoreCooldownMinutes)continue;
     var covered=new HashSet<string>(allChapters.SelectMany(c=>c.EventIds));
     var career=store.Events(world,new TimeWindow(DateTime.MinValue,DateTime.UtcNow),consent).Where(LoreEvent).OrderBy(e=>e.Utc).ThenBy(e=>e.Id,StringComparer.Ordinal).ToList();
     foreach(var e in career)if(string.IsNullOrWhiteSpace(e.PlayerName))e.PlayerName=sharers[e.PlayerId];
     var ev=career.Where(e=>!covered.Contains(e.Id)).ToList();if(!Milestone(ev))continue;
     if(!store.LoreReady(world,""))continue;
     var chapter=await engine.GenerateServerAsync(options.ServerName,ev.Take(100).ToArray(),previous,stop.Token,career,store.NarrativeContext(world));
     // A profile may be revoked while a remote generation was pending; never publish that response.
     var currentConsent=new HashSet<string>(store.Players(world).Where(p=>p.ShareProfile).Select(p=>p.PlayerId));
     if(GeneratedChapter(chapter)&&SharedChapterVisible(chapter,currentConsent))store.Chapter(chapter);else store.DeferLore(world,"");
    }
   }catch(OperationCanceledException){}catch(Exception e){diagnostics.Report("lore-worker",e);}
   try{await Task.Delay(10000,stop.Token);}catch(OperationCanceledException){break;}
  }
 }
 public object Health()=>new{rejected=Rejected,storageError=failure,errorId,queue=queue.Count};
 async Task WebLoop(){while(listener!=null&&listener.IsListening){HttpListenerContext context;try{context=await listener.GetContextAsync();}catch{break;}if(!httpSlots.Wait(0)){context.Response.StatusCode=503;context.Response.Close();continue;}_=Task.Run(async()=>{try{await Handle(context);}catch(Exception e){var id=diagnostics.Report("http-request",e);try{await Respond(context,500,new{error="Sagas could not read or serve the recorded data. Check the server diagnostics log using the error ID.",errorId=id});}catch{try{context.Response.Close();}catch{}}}finally{httpSlots.Release();}});}}
 static bool TokenEquals(string expected,string actual){var a=Encoding.UTF8.GetBytes(expected);var b=Encoding.UTF8.GetBytes(actual);int diff=a.Length^b.Length;for(int i=0;i<a.Length;i++)diff|=a[i]^(i<b.Length?b[i]:0);return diff==0;}
 async Task Handle(HttpListenerContext c){c.Response.Headers["X-Content-Type-Options"]="nosniff";c.Response.Headers["Referrer-Policy"]="no-referrer";c.Response.Headers["Cache-Control"]="no-store";c.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";
  string path=c.Request.Url!.AbsolutePath;
  if(c.Request.HttpMethod!="GET"&&!(c.Request.HttpMethod=="POST"&&(path=="/api/profile-background"||path=="/api/lore-settings"))){c.Response.StatusCode=405;c.Response.Close();return;}
  if(path=="/api/access"){await Respond(c,200,new{requiresToken=options.RequireViewerToken});return;}
  if(path.StartsWith("/api/",StringComparison.Ordinal)){
   var authorization=c.Request.Headers["Authorization"]??"";
   var identity=authorization.StartsWith("Bearer ",StringComparison.Ordinal)?store.AuthenticatePlayer(authorization.Substring(7)):null;
   var shared=options.ViewerToken.Length>=24&&TokenEquals("Bearer "+options.ViewerToken,authorization);
   if(identity==null&&!shared&&(options.RequireViewerToken||authorization!="")){await Respond(c,401,new{error="A valid login token is required."});return;}
   if(path=="/api/lore-settings"){await PersonalLoreApi(c,identity);return;}
   if(path=="/api/profile-background"){if(c.Request.HttpMethod!="POST"){await Respond(c,405,new{error="Use POST."});return;}await SaveBackground(c,identity);return;}
var q=c.Request.QueryString;string world=string.IsNullOrWhiteSpace(q["world"])?options.World:q["world"]!;if(world.Length>100){await Respond(c,400,new{error="Invalid world."});return;}HashSet<string>? ids=string.IsNullOrWhiteSpace(q["players"])?null:new HashSet<string>(q["players"]!.Split(',').Where(x=>x.Length>0));if(ids!=null&&ids.Count>100){await Respond(c,400,new{error="Too many players."});return;}
   try{if(path=="/api/session")await Respond(c,200,LoginSession(world,identity));else if(path.StartsWith("/api/media/",StringComparison.Ordinal))await ServeMedia(c,world,q["player"]??"",path.Substring(11));else if(path=="/api/health")await Respond(c,200,Health());else if(path=="/api/state")await Respond(c,200,State(world,TimeWindow.Parse(q["range"],q["from"],q["to"],DateTime.UtcNow),ids));else if(path=="/api/map")await Respond(c,200,Map(world,ids));else if(path=="/api/leaderboard")await Respond(c,200,Leaderboard(world,TimeWindow.Parse(q["range"],q["from"],q["to"],DateTime.UtcNow),ids));else if(path=="/api/server-saga")await Respond(c,200,ServerSaga(world));else await Respond(c,404,new{error="Not found"});}catch(ArgumentException e){await Respond(c,400,new{error=e.Message});}return;
  }
  // Explicit allow-list, never expose database/configuration or arbitrary filesystem paths.
  if(!StaticFiles.TryGetValue(path,out var file)||!File.Exists(Path.Combine(options.WebDirectory,file))){c.Response.StatusCode=404;c.Response.Close();return;}
  if(file.EndsWith(".webp",StringComparison.Ordinal))c.Response.Headers["Cache-Control"]="public, max-age=300";
  c.Response.ContentType=file.EndsWith(".webp")?"image/webp":file.EndsWith(".png")?"image/png":file.EndsWith(".js")?"text/javascript; charset=utf-8":file.EndsWith(".css")?"text/css; charset=utf-8":file.EndsWith(".svg")?"image/svg+xml":"text/html; charset=utf-8";var bytes=File.ReadAllBytes(Path.Combine(options.WebDirectory,file));c.Response.ContentLength64=bytes.Length;await c.Response.OutputStream.WriteAsync(bytes,0,bytes.Length);c.Response.Close();
 }
 // Construct the finite set once; no client path components become filesystem paths.
 static readonly Dictionary<string,string> StaticFiles=BuildStaticFiles();
 static Dictionary<string,string> BuildStaticFiles(){
  var files=new Dictionary<string,string>(StringComparer.Ordinal){{"/","index.html"},{"/index.html","index.html"},{"/app.js","app.js"},{"/styles.css","styles.css"},{"/style.css","style.css"},{"/favicon.svg","favicon.svg"}};
  foreach(var biome in new[]{"meadows","black-forest","swamp","mountain","plains","mistlands","ashlands","deep-north"}){
   if(biome!="deep-north"){var legacy="biomes/"+biome+".png";files.Add("/"+legacy,legacy);}
   foreach(var composition in new[]{"grounded"})foreach(var width in new[]{1920,2560,3840}){
    var file="biomes/"+composition+"/"+biome+"-"+width+".webp";files.Add("/"+file,file);
   }
  }
  return files;
 }
 async Task Respond(HttpListenerContext c,int code,object value){var bytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value,json));if(Volatile.Read(ref gzipDisabled)==0&&bytes.Length>1024&&(c.Request.Headers["Accept-Encoding"]??"").Split(',').Any(x=>x.Trim().Equals("gzip",StringComparison.OrdinalIgnoreCase))){try{using(var buffer=new MemoryStream()){using(var gzip=new GZipStream(buffer,CompressionLevel.Fastest,true))gzip.Write(bytes,0,bytes.Length);bytes=buffer.ToArray();}c.Response.Headers["Content-Encoding"]="gzip";c.Response.Headers["Vary"]="Accept-Encoding";}catch(Exception e) when(e is DllNotFoundException||e is EntryPointNotFoundException||e is NotSupportedException||e is TypeInitializationException){Interlocked.Exchange(ref gzipDisabled,1);diagnostics.Report("optional-http-compression-disabled",e);}}c.Response.StatusCode=code;c.Response.ContentType="application/json; charset=utf-8";c.Response.ContentLength64=bytes.Length;await c.Response.OutputStream.WriteAsync(bytes,0,bytes.Length);c.Response.Close();}
 public void Dispose(){if(disposed)return;disposed=true;listener?.Close();stop.Cancel();queue.CompleteAdding();writer?.Wait();try{lore?.Wait();}catch(AggregateException){}store.Dispose();stop.Dispose();}
}
