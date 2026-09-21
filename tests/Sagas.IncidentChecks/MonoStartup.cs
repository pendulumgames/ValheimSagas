using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace ValheimSagas.Incident;
public static partial class MonoEntry {
 public static void RunStartup(){
  var root=Environment.GetEnvironmentVariable("SAGAS_INCIDENT_OUTPUT")??throw new Exception("Missing isolated output.");if(Directory.Exists(root))throw new Exception("Refusing existing output.");Directory.CreateDirectory(root);
  using var report=new StreamWriter(Path.Combine(root,"report.txt"));
  var owner=new AsyncResource<SagaService>();int main=Thread.CurrentThread.ManagedThreadId,worker=0;var options=new SagaOptions{DataDirectory=root,World="synthetic-startup",ListenPrefix="",LoreEnabled=false};
  SagaService Create(){worker=Thread.CurrentThread.ManagedThreadId;var service=new SagaService(options);try{service.Start();return service;}catch{service.Dispose();throw;}}
  void Wait(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(10);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Startup lifecycle timed out.");Thread.Sleep(5);}}
  owner.Begin(Create);Wait(()=>owner.Poll());if(worker==main)throw new Exception("Storage constructed on caller thread.");
  owner.Current!.UpdatePlayer(new PlayerSnapshot{World=options.World,PlayerId="synthetic-player",Name="Synthetic startup Viking",Online=true,ShareProfile=true});
  owner.Retire();Wait(()=>!owner.Busy);owner.Begin(Create);Wait(()=>owner.Poll());
  var player=owner.Current!.Store.Players(options.World).Single();if(player.Online||player.PlayerId!="synthetic-player")throw new Exception("Worker disposal lost queued data or restart presence reset.");owner.Retire();Wait(()=>!owner.Busy);
  report.WriteLine("PASS actual Unity Mono worker construction/start, queued storage drain, asynchronous retirement and same-database reopen with persisted synthetic data. No graphics or game hooks executed.");
 }
}
