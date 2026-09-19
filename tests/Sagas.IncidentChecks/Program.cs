using LiteDB;
using Newtonsoft.Json;
using ValheimSagas;

// Never opens the supplied evidence directory. Every run makes a fresh working copy.
if(args.Length != 2) throw new ArgumentException("Usage: IncidentChecks <copied-evidence-directory> <new-isolated-working-directory>");
var evidence=Path.GetFullPath(args[0]);var working=Path.GetFullPath(args[1]);
if(Directory.Exists(working))throw new ArgumentException("Working directory must not exist.");
Directory.CreateDirectory(working);
foreach(var file in Directory.GetFiles(evidence))File.Copy(file,Path.Combine(working,Path.GetFileName(file)));
int failed=0;
void Check(string label,Action action){try{action();Console.WriteLine("PASS "+label);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+label+"\n"+ex);}}
Check("raw collection counts",()=>{using var db=new LiteDatabase(Path.Combine(working,"sagas.db"));foreach(var name in db.GetCollectionNames())Console.WriteLine(name+": "+db.GetCollection(name).Count());});
Check("SagaStore open, queries, replay, writes, checkpoint",()=>{
 using var store=new SagaStore(working);
 var worlds=store.Worlds();Console.WriteLine("World count: "+worlds.Length);
 foreach(var world in worlds){var players=store.Players(world);Console.WriteLine($"Players: {players.Count}; events: {store.Events(world,TimeWindow.Parse("all",null,null,DateTime.UtcNow)).Count}; cells: {store.Cells(world,players.Select(p=>p.PlayerId).ToHashSet()).Count}; chapters: {store.Chapters(world).Count}");foreach(var p in players)store.Player(p);}
 int accepted=0,total=0;
 foreach(var file in Directory.GetFiles(working,"outbox.*.json"))foreach(var e in JsonConvert.DeserializeObject<List<SagaEvent>>(File.ReadAllText(file))!){total++;if(store.AddEvent(e))accepted++;}
 Console.WriteLine($"Outbox events replayed: {total}; newly accepted: {accepted}");
 store.ApplyRetention(DateTime.UtcNow,0,0);store.Checkpoint();
});
Environment.ExitCode=failed==0?0:1;
