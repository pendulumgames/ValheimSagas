using ValheimSagas;
static class StartupChecks {
 sealed class Resource:IDisposable {internal int Disposals;public void Dispose(){Interlocked.Increment(ref Disposals);}}
 static async Task Until(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(5);while(!done()){if(DateTime.UtcNow>end)throw new Exception("Worker lifecycle timed out.");await Task.Delay(5);}}
 internal static async Task Run(Action<bool,string> check){
  var owner=new AsyncResource<Resource>();using var gate=new ManualResetEventSlim();var value=new Resource();
  check(owner.Begin(()=>{gate.Wait();return value;}),"Startup schedules worker");check(owner.Busy&&!owner.Poll()&&owner.Current==null,"Main thread does not wait for startup");
  check(!owner.Begin(()=>new Resource()),"Duplicate startup cannot open database twice");owner.Retire();check(owner.Current==null&&!owner.Begin(()=>new Resource()),"World exit retires pending startup without reopening");gate.Set();await Until(()=>!owner.Busy);check(value.Disposals==1,"Unadopted world startup is disposed exactly once");
  var active=new Resource();check(owner.Begin(()=>active),"New world starts after retirement");await Until(()=>owner.Poll());check(owner.Current==active,"Completed startup adopts correct resource");
  using var disposalGate=new ManualResetEventSlim();owner.Retire(_=>disposalGate.Wait());check(owner.Current==null&&owner.Busy&&!owner.Begin(()=>new Resource()),"Slow shutdown blocks reopening but never blocks caller");disposalGate.Set();await Until(()=>!owner.Busy);check(active.Disposals==1,"Adopted service disposed once");owner.Retire();check(active.Disposals==1,"Repeated world-exit cleanup is harmless");
  owner.Begin(()=>throw new InvalidOperationException("Synthetic startup failure"));bool observed=false;await Until(()=>{try{return owner.Poll();}catch(InvalidOperationException){observed=true;return true;}});check(observed&&!owner.Busy,"Startup failure is observable and retryable");
  var badShutdown=new Resource();owner.Begin(()=>badShutdown);await Until(()=>owner.Poll());owner.Retire(_=>throw new InvalidOperationException("Synthetic shutdown failure"));await Until(()=>!owner.Busy);
  try{owner.Begin(()=>new Resource());check(false,"Retirement failure must be observed");}catch(InvalidOperationException){check(badShutdown.Disposals==1,"Cleanup error still disposes resource and is observed");}
  var retry=new Resource();owner.Begin(()=>retry);await Until(()=>owner.Poll());owner.Retire();await Until(()=>!owner.Busy);check(retry.Disposals==1,"Retry works after failed construction");
 }
}
