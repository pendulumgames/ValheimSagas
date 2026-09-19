using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
namespace ValheimSagas;
// Small private client journal. No credentials, inventory contents, or account identifiers.
internal sealed class Outbox {
 readonly string file; readonly Action<string>? log; bool warned; Task? write;
 internal Outbox(string directory,string world,Action<string>? log=null){this.log=log;Directory.CreateDirectory(directory);file=Path.Combine(directory,"outbox."+world+".json");}
 internal List<SagaEvent> Load(){try{return JsonConvert.DeserializeObject<List<SagaEvent>>(File.ReadAllText(file))??new List<SagaEvent>();}catch{return new List<SagaEvent>();}}
 internal void Save(IEnumerable<SagaEvent> events){if(write!=null&&!write.IsCompleted)return;var json=JsonConvert.SerializeObject(events);write=Task.Run(()=>{try{var tmp=file+".tmp";File.WriteAllText(tmp,json);if(File.Exists(file))File.Replace(tmp,file,null);else File.Move(tmp,file);}catch{if(!warned){warned=true;log?.Invoke("Sagas could not write the local retry journal. Events continue retrying in memory; a crash may lose pending telemetry. Check data-directory permissions and disk space.");}}});}
 internal void Finish(IEnumerable<SagaEvent> events){try{write?.Wait(2000);Save(events);write?.Wait(2000);}catch{}}
}
