using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValheimSagas;

// SYNTHETIC ONLY: no game/mod assets, world files, credentials, or listener are used.
var output=Path.GetFullPath(Path.Combine(".dev","benchmark",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)));
Directory.CreateDirectory(output);
var timestamp=DateTime.UtcNow;
var now=new DateTime(timestamp.Ticks-timestamp.Ticks%TimeSpan.TicksPerMinute,DateTimeKind.Utc);
var all=new TimeWindow(DateTime.MinValue,now);
var seven=TimeWindow.Parse("7d",null,null,now);
var selected=new HashSet<string>{"p0","p1"};
var total=Stopwatch.StartNew();
var metrics=new Dictionary<string,object>{["synthetic"]=true,["utc"]=DateTime.UtcNow,["runtime"]=RuntimeInformation.FrameworkDescription,["os"]=RuntimeInformation.OSDescription,["architecture"]=RuntimeInformation.ProcessArchitecture.ToString(),["logicalProcessors"]=Environment.ProcessorCount,["processor"]=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")??"not reported",["directory"]=output};
long Memory()=>GC.GetTotalMemory(true);
void Check(bool ok,string message){if(!ok)throw new Exception("Benchmark verification failed: "+message);}
double Median(List<double> samples){samples.Sort();return samples[samples.Count/2];}
var beforeMemory=Memory();var beforeWorking=Process.GetCurrentProcess().WorkingSet64;var beforeAlloc=GC.GetTotalAllocatedBytes(true);
using(var service=new SagaService(new(){DataDirectory=output,ListenPrefix="",Synthetic=true})) {
 // Store and query paths are benchmarked directly; no background lore worker or HTTP listener.
 for(int p=0;p<10;p++)service.Store.Player(new(){World="synthetic-benchmark",PlayerId="p"+p,Name="Synthetic Viking "+p,ShareMap=true,ShareProfile=true,Online=true});
 var watch=Stopwatch.StartNew();
 for(int i=0;i<10000;i++) {
  int group=i/5, slot=i%5;var kind=slot<3?"kill":slot==3?"drop":"collect";
  var e=new SagaEvent{Id="synthetic-"+i,World="synthetic-benchmark",PlayerId="p"+(group%10),PlayerName="Synthetic Viking "+(group%10),Utc=now.AddDays(-(group%10)).AddMinutes(-30),Kind=kind,Prefab=slot<3?"Troll":"Coins",Name=slot<3?"Synthetic Troll":"Synthetic Coins",Stars=group%6,Amount=slot<3?1:5,Provenance=slot<3?"":"synthetic-item-"+group,Quality=1,Rarity="",X=(group%50)*64+32,Z=(group%20)*64+32,Source="synthetic-fixture"};
  Check(service.Store.AddEvent(e),"all distinct input events accepted");
 }
 watch.Stop();metrics["insert10000Ms"]=watch.Elapsed.TotalMilliseconds;metrics["eventsPerSecond"]=10000/watch.Elapsed.TotalSeconds;
 var cellSamples=new List<double>();watch.Restart();
 for(int p=0;p<2;p++)service.Store.Explore(new(){World="synthetic-benchmark",PlayerId="p"+p,Imported=true,Cells=Enumerable.Range(p*250,750).Select(i=>new MapCell{X=i%50,Z=i/50,Biome="Synthetic Meadows",Height=i%100}).ToList()});
 watch.Stop();metrics["insert1500CellContributionsMs"]=watch.Elapsed.TotalMilliseconds;
 for(int i=0;i<5;i++){watch.Restart();var map=service.Map("synthetic-benchmark",selected);watch.Stop();cellSamples.Add(watch.Elapsed.TotalMilliseconds);Check(JObject.FromObject(map)["cells"]!.Count()==1000,"combined map union exactly 1000 cells");}
 metrics["mapUnionMedianMs"]=Median(cellSamples);
 foreach(var scenario in new[]{("all",all,(HashSet<string>?)null,10000,6000,10000), ("sevenDays",seven,(HashSet<string>?)null,7000,4200,7000), ("sevenDaysTwoPlayers",seven,(HashSet<string>?)selected,2000,1200,2000)}) {
  var samples=new List<double>();object? result=null;
  for(int i=0;i<6;i++) {watch.Restart();result=service.State("synthetic-benchmark",scenario.Item2,scenario.Item3);watch.Stop();if(i==0)metrics[scenario.Item1+"FirstQueryMs"]=watch.Elapsed.TotalMilliseconds;else samples.Add(watch.Elapsed.TotalMilliseconds);}
  metrics[scenario.Item1+"WarmMedianMs"]=Median(samples);
  var json=JObject.FromObject(result!);Check((int)json["eventCount"]! ==scenario.Item4,"filtered event count "+scenario.Item1);Check((int)json["stats"]!["kills"]! ==scenario.Item5,"filtered kills "+scenario.Item1);Check((long)json["stats"]!["dropped"]! ==scenario.Item6&&(long)json["stats"]!["collected"]! ==scenario.Item6,"drop/earned distinction "+scenario.Item1);
  metrics[scenario.Item1+"EventCount"]=scenario.Item4;metrics[scenario.Item1+"SerializedBytes"]=System.Text.Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(result));
  Check(json["events"]!.Count()==Math.Min(5000,scenario.Item4),"display cap does not affect exact totals");
 }
 service.Store.Checkpoint();metrics["databaseBytesAfterCheckpoint"]=new FileInfo(Path.Combine(output,"sagas.db")).Length;
 metrics["managedBytesBefore"]=beforeMemory;metrics["managedBytesAfterQueries"]=Memory();metrics["processWorkingSetBytesBefore"]=beforeWorking;metrics["processWorkingSetBytesAfter"]=Process.GetCurrentProcess().WorkingSet64;metrics["managedBytesAllocatedDuringRun"]=GC.GetTotalAllocatedBytes(true)-beforeAlloc;
}
using(var restored=new SagaStore(output)) {
 var persisted=restored.Events("synthetic-benchmark",all);
 Check(persisted.Count==10000&&persisted.Count(e=>e.Kind=="kill")==6000,"all totals survive restart");
 Check(persisted.Where(e=>e.Kind=="collect").Sum(e=>e.Amount)==10000,"collection ledger survives restart");
 Check(restored.Cells("synthetic-benchmark",selected).Count==1000,"map union survives restart");
 Check(restored.Players("synthetic-benchmark").All(p=>!p.Online),"restart does not resurrect presence");
 Check(!restored.AddEvent(new(){Id="synthetic-0",World="synthetic-benchmark"}),"restart dedup remains effective");
}
metrics["verification"]="PASS: exact full/7d/player totals, separate dropped/collected totals, 1000-cell union, restart, presence reset, persistent dedup";
metrics["elapsedSeconds"]=total.Elapsed.TotalSeconds;
var report=JsonConvert.SerializeObject(metrics,Formatting.Indented);File.WriteAllText(Path.Combine(output,"results.json"),report);Console.WriteLine(report);
