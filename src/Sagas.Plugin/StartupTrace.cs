using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace ValheimSagas;
internal sealed class StartupTrace:IDisposable {
 readonly Stopwatch? timer;readonly Action<string> log;readonly string name;readonly List<string> parts=new List<string>();double previous;
 internal StartupTrace(bool enabled,string name,Action<string> log){this.name=name;this.log=log;if(enabled)timer=Stopwatch.StartNew();}
 internal void Mark(string phase){if(timer==null)return;var now=timer.Elapsed.TotalMilliseconds;parts.Add(phase+"="+(now-previous).ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" ms");previous=now;}
 public void Dispose(){if(timer!=null)log("Sagas startup detail "+name+": "+string.Join(", ",parts)+"; measured body="+timer.Elapsed.TotalMilliseconds.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" ms (first-use JIT before entry is not included).");}
}
