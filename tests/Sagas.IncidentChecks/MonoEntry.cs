using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
namespace ValheimSagas.Incident;
public static partial class MonoEntry {
 public static void RunEvidence(){
  string output=Environment.GetEnvironmentVariable("SAGAS_INCIDENT_OUTPUT")??throw new Exception("Set SAGAS_INCIDENT_OUTPUT to a new isolated directory.");
  string evidence=Environment.GetEnvironmentVariable("SAGAS_INCIDENT_EVIDENCE")??throw new Exception("Set SAGAS_INCIDENT_EVIDENCE to an already copied evidence directory.");
  if(Directory.Exists(output))throw new Exception("Output already exists; refusing overwrite.");
  Directory.CreateDirectory(output);foreach(var f in Directory.GetFiles(evidence))File.Copy(f,Path.Combine(output,Path.GetFileName(f)));
  using(var report=new StreamWriter(Path.Combine(output,"report.txt"))){report.AutoFlush=true;
   try{int worlds=0,players=0,cells=0,events=0,replayed=0;using(var store=new SagaStore(output)){
    foreach(var world in store.Worlds()){worlds++;var ps=store.Players(world);players+=ps.Count;cells+=store.Cells(world,new HashSet<string>(ps.Select(p=>p.PlayerId))).Count;events+=store.Events(world,TimeWindow.Parse("all",null,null,DateTime.UtcNow)).Count;foreach(var p in ps)store.Player(p);}
    foreach(var f in Directory.GetFiles(output,"outbox.*.json"))foreach(var e in JsonConvert.DeserializeObject<List<SagaEvent>>(File.ReadAllText(f))!)if(store.AddEvent(e))replayed++;
    store.Checkpoint();report.WriteLine($"PASS evidence recovery: worlds={worlds}; players={players}; cells={cells}; initial events={events}; replayed={replayed}");
   }
   using(var reopened=new SagaStore(output)){
    int after=reopened.Worlds().Sum(w=>reopened.Events(w,TimeWindow.Parse("all",null,null,DateTime.UtcNow)).Count),again=0;
    foreach(var f in Directory.GetFiles(output,"outbox.*.json"))foreach(var e in JsonConvert.DeserializeObject<List<SagaEvent>>(File.ReadAllText(f))!)if(reopened.AddEvent(e))again++;
    if(after!=events+replayed||again!=0)throw new Exception($"Recovery invariant failed: before={events}; accepted={replayed}; persisted={after}; duplicate accepted={again}");
    report.WriteLine($"PASS reopened event total={after}; duplicate replay accepted={again}");
   }}catch(Exception ex){report.WriteLine(ex);throw;}
  }
 }
 public static void Run(){
  string output=Environment.GetEnvironmentVariable("SAGAS_INCIDENT_OUTPUT")??throw new Exception("Set SAGAS_INCIDENT_OUTPUT to a new isolated directory.");
  if(Directory.Exists(output))throw new Exception("Output already exists; refusing overwrite.");
  Directory.CreateDirectory(output);
  using(var report=new StreamWriter(Path.Combine(output,"report.txt"))){report.AutoFlush=true;report.WriteLine("Runtime: "+Environment.Version+"; Mono="+(Type.GetType("Mono.Runtime")!=null));
   try{using(var store=new SagaStore(output)){
    for(int batch=0;batch<100;batch++){
     var b=new ExplorationBatch{World="synthetic-incident",PlayerId="synthetic-player",Cells=new List<MapCell>()};
     for(int i=0;i<128;i++)b.Cells.Add(new MapCell{X=(batch*128+i)%250,Z=(batch*128+i)/250,Biome="Meadows",Height=1});
     store.Explore(b);store.Player(new PlayerSnapshot{World=b.World,PlayerId=b.PlayerId,Name="Synthetic fixture",Utc=DateTime.UtcNow,Online=true,ShareMap=true});
     report.WriteLine("Committed cells="+((batch+1)*128));
    }
    store.Checkpoint();report.WriteLine("PASS repeated automatic checkpoints and explicit checkpoint");
   }
   using(var reopened=new SagaStore(output)){
    int cells=reopened.Cells("synthetic-incident",new HashSet<string>{"synthetic-player"}).Count;
    if(cells!=12800)throw new Exception("Expected 12800 persisted cells, observed "+cells);
    report.WriteLine("PASS reopened synthetic cell count="+cells);
   }}catch(Exception ex){report.WriteLine(ex.ToString());throw;}
  }
 }
}
