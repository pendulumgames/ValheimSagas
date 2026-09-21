using System;
using System.Threading.Tasks;
namespace ValheimSagas;
// Main-thread ownership with construction/disposal on workers. Never reopen while retirement is in flight.
internal sealed class AsyncResource<T> where T:class,IDisposable {
 Task<T>? pending;Task retirement=Task.CompletedTask;
 internal T? Current {get;private set;}
 internal bool Busy=>pending!=null||!retirement.IsCompleted;
 internal bool Begin(Func<T> factory){if(Current!=null||Busy)return false;var finished=retirement;retirement=Task.CompletedTask;finished.GetAwaiter().GetResult();pending=Task.Run(factory);return true;}
 internal bool Poll(){if(pending==null||!pending.IsCompleted)return false;var task=pending;pending=null;Current=task.GetAwaiter().GetResult();return true;}
 internal void Retire(Action<T>? beforeDispose=null){var value=Current;var creating=pending;if(value==null&&creating==null)return;Current=null;pending=null;var previous=retirement;retirement=Task.Run(async()=>{await previous.ConfigureAwait(false);T? resource=value;if(creating!=null){try{resource=await creating.ConfigureAwait(false);}catch{return;}}if(resource!=null)try{beforeDispose?.Invoke(resource);}finally{resource.Dispose();}});}
}
