using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
namespace ValheimSagas;

// Server-local detail, public correlation IDs. Never send exception stacks or secrets to browsers.
public sealed class SagaDiagnostics {
 readonly SagaOptions options; readonly object gate=new object(); readonly Dictionary<string,Entry> recent=new Dictionary<string,Entry>();
 sealed class Entry{public DateTime Utc;public string Id="";public int Suppressed;}
 public SagaDiagnostics(SagaOptions options){this.options=options;}
 public string Report(string area,Exception error){lock(gate){
  var now=DateTime.UtcNow;var detail=Redact(error.ToString());var key=area+":"+detail;
  recent.TryGetValue(key,out var old);
  if(old!=null&&(now-old.Utc).TotalSeconds<30){old.Suppressed++;return old.Id;}
  var id=Guid.NewGuid().ToString("N").Substring(0,12);var message=$"Sagas error {id} [{area}] {now:O}"+(old!=null&&old.Suppressed>0?$" ({old.Suppressed} repeated messages suppressed)":"")+Environment.NewLine+detail;
  if(recent.Count>=64)recent.Remove(recent.OrderBy(p=>p.Value.Utc).First().Key);
  recent[key]=new Entry{Utc=now,Id=id};
  try{options.Log?.Invoke(message);}catch{ /* A log sink must not take down collection. */ }
  try{Directory.CreateDirectory(options.DataDirectory);var file=Path.Combine(options.DataDirectory,"diagnostics.log");if(File.Exists(file)&&new FileInfo(file).Length>2*1024*1024){var previous=file+".previous";if(File.Exists(previous))File.Delete(previous);File.Move(file,previous);}File.AppendAllText(file,message+Environment.NewLine);}catch{ /* BepInEx log is still the fallback when the data path is unwritable. */ }
  return id;
 }}
 string Redact(string text){foreach(var secret in new[]{options.ViewerToken,options.OpenRouterKey})if(!string.IsNullOrEmpty(secret))text=text.Replace(secret,"[redacted]");return text;}
}
