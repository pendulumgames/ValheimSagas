using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using Newtonsoft.Json;
namespace ValheimSagas;
// Single process, transactional managed database; IDs survive retention for retry deduplication.
public sealed partial class SagaStore : IDisposable {
 readonly LiteDatabase db; readonly object gate = new object();
 public DateTime TrackingSince {get;}
 public SagaStore(string directory) {
  Directory.CreateDirectory(directory);secretDirectory=directory;
  db=new LiteDatabase(Path.Combine(directory,"sagas.db"));
  var meta=db.GetCollection("meta"); var version=meta.FindById("schema");
  if(version!=null && version["value"].AsInt32>1)throw new InvalidOperationException("Database is newer than this plugin.");
  meta.Upsert(new BsonDocument{{"_id","schema"},{"value",1}});
  var start=meta.FindById("tracking"); TrackingSince=start==null?DateTime.UtcNow:start["utc"].AsDateTime.ToUniversalTime();
  if(start==null)meta.Insert(new BsonDocument{{"_id","tracking"},{"utc",TrackingSince}});
  foreach(var c in new[]{"events","statistics","players","cells","chapters","serverchapters","media","mapPins","vikingColors"})db.GetCollection(c).EnsureIndex("world");
  db.GetCollection("statistics").EnsureIndex("utc");db.GetCollection("statistics").EnsureIndex("player");
  db.GetCollection("events").EnsureIndex("utc");db.GetCollection("events").EnsureIndex("player");
  db.GetCollection("cells").EnsureIndex("player");
  db.GetCollection("pendingCollections").EnsureIndex("provenance");
  db.GetCollection("profileLinks").EnsureIndex("slug",true);
  db.GetCollection("bossReceipts").EnsureIndex("owner");
  db.GetCollection("playerLogins").EnsureIndex("hash",true);
  if(meta.FindById("bossReceiptsMigrationV2")==null){foreach(var row in db.GetCollection("events").FindAll().Concat(db.GetCollection("statistics").FindAll()))RecordBossReceipt(Read<SagaEvent>(row));meta.Upsert(new BsonDocument{{"_id","bossReceiptsMigrationV2"},{"value",1}});}
  // Migrate existing profiles and reserve stable server-wide readable links.
  // No connection is online after a process restart.
  foreach(var d in db.GetCollection("players").FindAll().ToArray()) {var p=Read<PlayerSnapshot>(d);AssignProfileSlug(p);AssignMapColor(p);p.Online=false;d["json"]=JsonConvert.SerializeObject(p);db.GetCollection("players").Update(d);}
 }
 static string Key(params string[] parts)=>string.Join("|",parts.Select(x=>Uri.EscapeDataString(x)));
 static BsonDocument Wrap(string id,string world,string player,object value,DateTime utc)=>new BsonDocument{{"_id",id},{"world",world},{"player",player},{"utc",utc},{"json",JsonConvert.SerializeObject(value)}};
 static T Read<T>(BsonDocument d)=>JsonConvert.DeserializeObject<T>(d["json"].AsString)!;
 public bool AddEvent(SagaEvent e) { lock(gate) {
  string id=Key(e.World,e.Id);var seen=db.GetCollection("seen"); if(seen.Exists(Query.EQ("_id",id)))return false;
  if(e.Kind=="collect"&&string.IsNullOrEmpty(e.Provenance))return false;
  // Collection IDs use a stable provenance ID. Quantity is capped by the actual emitted drop,
  // so partial stacks/repeated pickup reports can never mint additional earned loot.
  db.BeginTrans();try {
   if(e.Kind=="collect" && !string.IsNullOrEmpty(e.Provenance)) {
    string pid=Key(e.World,e.Provenance);var ledger=db.GetCollection("provenance");var p=ledger.FindById(pid);
    if(p==null) {
     // A client can observe pickup before the owning client's drop reaches the server.
     // Persist both receipt and pending payload in one commit before allowing an ACK.
     var pending=Wrap(id,e.World,e.PlayerId,e,e.Utc);pending["provenance"]=pid;
     db.GetCollection("pendingCollections").Insert(pending);seen.Insert(new BsonDocument{{"_id",id}});db.Commit();return true;
    }
    if(p["dropped"].AsInt32<=p["collected"].AsInt32) {seen.Insert(new BsonDocument{{"_id",id}});db.Commit();return false;}
    e.Amount=Math.Min(e.Amount,p["dropped"].AsInt32-p["collected"].AsInt32);if(p.ContainsKey("json"))CopyLootMetadata(e,Read<SagaEvent>(p));p["collected"]=p["collected"].AsInt32+e.Amount;ledger.Update(p);
   }
   if(e.Kind=="drop" && !string.IsNullOrEmpty(e.Provenance)) {
    var ledger=db.GetCollection("provenance");string pid=Key(e.World,e.Provenance);
    if(ledger.FindById(pid)!=null){db.Rollback();return false;}
    var metadata=new SagaEvent{Prefab=e.Prefab,Name=e.Name,ItemType=e.ItemType,Quality=e.Quality,Rarity=e.Rarity,RarityColor=e.RarityColor,Source=e.Source,Effects=new List<string>(e.Effects)};
    var entry=new BsonDocument{{"_id",pid},{"dropped",e.Amount},{"collected",0},{"json",JsonConvert.SerializeObject(metadata)}};ledger.Insert(entry);
    var pending=db.GetCollection("pendingCollections");int remaining=e.Amount;
    foreach(var row in pending.Find(Query.EQ("provenance",pid)).OrderBy(d=>d["utc"].AsDateTime).ThenBy(d=>d["_id"].AsString,StringComparer.Ordinal).ToArray()) {
     var collected=Read<SagaEvent>(row);CopyLootMetadata(collected,e);collected.Amount=Math.Min(collected.Amount,remaining);
     if(collected.Amount>0) {remaining-=collected.Amount;db.GetCollection("events").Insert(Wrap(row["_id"].AsString,collected.World,collected.PlayerId,collected,collected.Utc));}
     pending.Delete(row["_id"]);
    }
    entry["collected"]=e.Amount-remaining;ledger.Update(entry);
   }
   seen.Insert(new BsonDocument{{"_id",id}});db.GetCollection("events").Insert(Wrap(id,e.World,e.PlayerId,e,e.Utc));RecordBossReceipt(e);db.Commit();return true;
  }catch{db.Rollback();throw;}
 }}
 public void Player(PlayerSnapshot p){lock(gate){AssignProfileSlug(p);AssignMapColor(p);db.GetCollection("players").Upsert(Wrap(Key(p.World,p.PlayerId),p.World,p.PlayerId,p,p.Utc));}}
 public void Explore(ExplorationBatch b) {lock(gate) {db.BeginTrans();try {foreach(var c in b.Cells){var cells=db.GetCollection("cells");var id=Key(b.World,b.PlayerId,c.X.ToString(),c.Z.ToString());var old=cells.FindById(id);cells.Upsert(new BsonDocument{{"_id",id},{"world",b.World},{"player",b.PlayerId},{"imported",b.Imported||(old!=null&&old["imported"].AsBoolean)},{"json",JsonConvert.SerializeObject(c)}});}db.Commit();}catch{db.Rollback();throw;}}}
 static void CopyLootMetadata(SagaEvent collected,SagaEvent drop){collected.Prefab=drop.Prefab;collected.Name=drop.Name;collected.ItemType=drop.ItemType;collected.Quality=drop.Quality;collected.Rarity=drop.Rarity;collected.RarityColor=drop.RarityColor;collected.Effects=new List<string>(drop.Effects);collected.Source=drop.Source;}
 public List<SagaEvent> Events(string world, TimeWindow w, HashSet<string>? players=null){lock(gate)return new[]{"events","statistics"}.SelectMany(c=>db.GetCollection(c).Find(Query.And(Query.EQ("world",world),Query.Between("utc",w.From,w.To)))).Select(Read<SagaEvent>).Where(e=>w.Contains(e.Utc)&&(players==null||players.Contains(e.PlayerId))).ToList();}
 public DateTime? StatisticsSince {get{lock(gate){var d=db.GetCollection("meta").FindById("statisticsSince");return d==null?(DateTime?)null:d["utc"].AsDateTime.ToUniversalTime();}}}
 // Retain exact timestamped statistical facts instead of approximate daily buckets: arbitrary
 // custom windows remain exact after detailed coordinates/effects have expired.
 public int ApplyRetention(DateTime now,int detailDays,int statisticsDays){lock(gate){if(detailDays<0||statisticsDays<0||(detailDays>0&&statisticsDays>0&&statisticsDays<detailDays))throw new ArgumentException("Statistics retention must be unlimited or at least detail retention.");int changed=0;db.BeginTrans();try{
  if(detailDays>0)foreach(var row in db.GetCollection("events").Find(Query.LT("utc",now.AddDays(-detailDays))).Take(1000).ToArray()) {var e=Read<SagaEvent>(row);e.X=null;e.Z=null;e.Effects.Clear();if(!e.Boss&&!e.NemesisBoss)e.Contributors.Clear();e.PlayerName="";e.Source="retained-statistical-fact";e.Provenance="";e.DetailsExpired=true;db.GetCollection("statistics").Upsert(Wrap(row["_id"].AsString,e.World,e.PlayerId,e,e.Utc));db.GetCollection("events").Delete(row["_id"]);changed++;}
  if(statisticsDays>0){var cutoff=now.AddDays(-statisticsDays);foreach(var name in new[]{"events","statistics"})changed+=db.GetCollection(name).DeleteMany(Query.LT("utc",cutoff));var previous=StatisticsSince;if(!previous.HasValue||cutoff>previous.Value)db.GetCollection("meta").Upsert(new BsonDocument{{"_id","statisticsSince"},{"utc",cutoff}});}
  // Dedup IDs/provenance and pending unmatched collections remain durable; they must never
  // reset collection ceilings. Completed saga factual ledgers survive event retention.
  db.Commit();return changed;
 }catch{db.Rollback();throw;}}}
 public List<PlayerSnapshot> Players(string world){lock(gate)return db.GetCollection("players").Find(Query.EQ("world",world)).Select(Read<PlayerSnapshot>).ToList();}
 public string[] Worlds(){lock(gate)return db.GetCollection("players").FindAll().Select(d=>d["world"].AsString).Concat(db.GetCollection("events").FindAll().Select(d=>d["world"].AsString)).Distinct().OrderBy(x=>x).ToArray();}
 public List<MapCell> Cells(string world,HashSet<string> players){lock(gate)return db.GetCollection("cells").Find(Query.EQ("world",world)).Where(d=>players.Contains(d["player"].AsString)).Select(Read<MapCell>).GroupBy(c=>Key(c.X.ToString(),c.Z.ToString())).Select(g=>TerrainTile.Union(g)).ToList();}
 public bool Imported(string world,HashSet<string> players){lock(gate)return db.GetCollection("cells").Find(Query.EQ("world",world)).Any(d=>players.Contains(d["player"].AsString)&&d["imported"].AsBoolean);}
 public List<SagaChapter> Chapters(string world){lock(gate)return db.GetCollection("chapters").Find(Query.EQ("world",world)).Select(Read<SagaChapter>).OrderBy(c=>c.Utc).ToList();}
 public void Chapter(SagaChapter c){lock(gate){if(c.Scope=="server"){var chapters=db.GetCollection("serverchapters");var id=Key(c.World,c.Id);if(chapters.FindById(id)==null)chapters.Insert(Wrap(id,c.World,"",c,c.Utc));}else db.GetCollection("chapters").Upsert(Wrap(Key(c.World,c.Id),c.World,c.PlayerId,c,c.Utc));}}
 public bool ReserveLoreRequest(DateTime utc,int max){lock(gate){var c=db.GetCollection("budget");string day=utc.ToString("yyyy-MM-dd");var d=c.FindById(day)??new BsonDocument{{"_id",day},{"count",0}};if(d["count"].AsInt32>=max)return false;d["count"]=d["count"].AsInt32+1;c.Upsert(d);return true;}}
 public void QueueLore(string world,string player){lock(gate){db.GetCollection("lorequeue").Upsert(new BsonDocument{{"_id",Key(world,player)},{"world",world},{"player",player}});QueueServerLore(world);}}
 public List<SagaChapter> ServerChapters(string world){lock(gate)return db.GetCollection("serverchapters").Find(Query.EQ("world",world)).Select(Read<SagaChapter>).OrderBy(c=>c.Utc).ThenBy(c=>c.Id,StringComparer.Ordinal).ToList();}
 public void QueueServerLore(string world){if(string.IsNullOrWhiteSpace(world))return;lock(gate)db.GetCollection("serverlorequeue").Upsert(new BsonDocument{{"_id",Key(world)},{"world",world}});}
 public string[] PendingServerLore(){lock(gate)return db.GetCollection("serverlorequeue").FindAll().Select(d=>d["world"].AsString).ToArray();}
 public (string World,string Player)[] PendingLore(){lock(gate)return db.GetCollection("lorequeue").FindAll().Select(d=>(d["world"].AsString,d["player"].AsString)).ToArray();}
 public void CompleteLore(string world,string player){lock(gate)db.GetCollection("lorequeue").Delete(Key(world,player));}
 public void Checkpoint(){lock(gate)db.Checkpoint();}
 public void Dispose(){lock(gate)db.Dispose();}
}
