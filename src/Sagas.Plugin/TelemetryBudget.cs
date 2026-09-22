using System;
namespace ValheimSagas;

// Separate byte allowances keep historical map/media imports from starving live
// snapshots or recorded events. All lanes yield to the game's existing queue.
internal sealed class TelemetryBudget {
 internal const int Events=0, Profile=1, Map=2, Artwork=3;
 internal static bool SteamHasHeadroom(int total,int unacknowledged,long queueMicroseconds) => total>=0&&unacknowledged>=0&&unacknowledged<=total&&total<=32768-4096&&total-unacknowledged<2048&&queueMicroseconds>=0&&queueMicroseconds<20000;
 internal const int MaximumPacketBytes=128000, QueueThreshold=8192;
 static readonly int[] Rates={8192,4096,3072,8192};
 readonly double[] credits=new double[4];
 double updated,nextSend;
 internal void Reset(double now){Array.Clear(credits,0,credits.Length);updated=now;nextSend=now;}
 internal bool Ready(int lane,int bytes,double now,int queued){
  var elapsed=Math.Max(0,now-updated);updated=now;
  for(int i=0;i<credits.Length;i++)credits[i]=Math.Min(MaximumPacketBytes,credits[i]+elapsed*Rates[i]);
  return bytes>0&&bytes<=MaximumPacketBytes&&queued>=0&&queued<QueueThreshold&&now>=nextSend&&credits[lane]>=bytes;
 }
 internal bool TrySpend(int lane,int bytes,double now,int queued){
  if(!Ready(lane,bytes,now,queued))return false;
  credits[lane]-=bytes;nextSend=now+.25;return true;
 }
}
