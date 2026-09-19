using System;
namespace ValheimSagas;

// Pure policy for a ZDO-persisted, shared-world-clock encounter timer.
internal static class BossTiming {
 internal static long ObserveDamage(long previous,float before,float maximum,long now){
  if(now<=0||float.IsNaN(before)||float.IsNaN(maximum)||float.IsInfinity(before)||float.IsInfinity(maximum)||maximum<=0)return -1;
  // A fully healed boss starts a new attempt. Never time an already damaged
  // boss from the moment a Sagas owner first sees it.
  if(before>=maximum-.01f)return now;
  return previous==0?-1:previous;
 }
 internal static double? Elapsed(long started,long now){
  if(started<=0||now<=started)return null;
  double seconds=(now-started)/1000.0;
  return seconds<=7*24*60*60?seconds:(double?)null;
 }
}
