using System;
namespace ValheimSagas.Incident;
public static partial class MonoEntry {
 public static void RunSlsCapture(){int checks=0;SlsCaptureChecks.Run((ok,message)=>{if(!ok)throw new Exception(message);checks++;});Console.WriteLine("PASS installed Unity Mono SLS capture: "+checks+" synthetic assertions; no game session.");}
}
