using System;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using Steamworks;
using UnityEngine;
namespace ValheimSagas;
public sealed partial class SagasPlugin {
 const string FragmentRpc="Sagas.Wire.V4";
 readonly System.Collections.Generic.Dictionary<ZRpc,(float start,int count)> fragmentRates=new System.Collections.Generic.Dictionary<ZRpc,(float start,int count)>();
 readonly WireTransfer wireReceiver=new WireTransfer();
 sealed class PendingWire { public Packet Packet=null!; public System.Threading.Tasks.Task<WirePayload> Preparation=null!; public string Id=Guid.NewGuid().ToString("N"); public int Index,Lane; }
 readonly System.Collections.Generic.Dictionary<int,PendingWire> outgoingWires=new System.Collections.Generic.Dictionary<int,PendingWire>();
 int wireTurn;
 static readonly System.Reflection.FieldInfo? steamConnection=AccessTools.Field(typeof(ZSteamSocket),"m_con");
 internal static long PeerPlayerId(ZNetPeer peer){
  if(!peer.IsReady())return 0;
  if(ZDOMan.instance!=null&&!peer.m_characterID.IsNone()){
   var zdo=ZDOMan.instance.GetZDO(peer.m_characterID);
   if(zdo!=null)return PeerIdentityPolicy.Resolve(peer.IsReady(),zdo.GetPrefab()=="Player".GetStableHashCode(),peer.m_uid,zdo.GetOwner(),zdo.GetLong(ZDOVars.s_playerID,0),peer.m_playerID);
  }
  return 0;
 }
 float pressureSince=-1,pressureLog;
 int QueueResult(int gate,int total,string detail){
  if(gate>=TelemetryBudget.QueueThreshold){if(pressureSince<0)pressureSince=Time.unscaledTime;if(Time.unscaledTime>=pressureLog){pressureLog=Time.unscaledTime+60;Logger.LogWarning("Sagas uploads paused: outstanding="+total+" bytes; "+detail+". Gameplay headroom reserved; pending uploads retained.");}}
  else if(pressureSince>=0){if(Time.unscaledTime-pressureSince>=3)Logger.LogInfo("Sagas uploads resumed after "+(Time.unscaledTime-pressureSince).ToString("F1")+" seconds; outstanding="+total+" bytes.");pressureSince=-1;}
  return gate;
 }
 int UploadQueue(ZNetPeer peer){
  int total=peer.m_socket.GetSendQueueSize();
  try {if(peer.m_socket is ZSteamSocket steam&&steamConnection?.GetValue(steam) is HSteamNetConnection connection){
   var status=new SteamNetConnectionRealTimeStatus_t();var lane=new SteamNetConnectionRealTimeLaneStatus_t();
   if(SteamNetworkingSockets.GetConnectionRealTimeStatus(connection,ref status,0,ref lane)==EResult.k_EResultOK){
    // Exclude ordinary ACK latency, but retain a strict total ceiling and reserve gameplay headroom.
    return QueueResult(TelemetryBudget.SteamHasHeadroom(total,status.m_cbSentUnackedReliable,(long)status.m_usecQueueTime)?0:Math.Max(TelemetryBudget.QueueThreshold,total),total,"Steam pending="+Math.Max(0,total-status.m_cbSentUnackedReliable)+", unacknowledged="+status.m_cbSentUnackedReliable+", queue delay us="+(long)status.m_usecQueueTime);
   }
  }}catch { /* Unsupported transport/version retains conservative queue gating. */ }
  return QueueResult(total,total,peer.m_socket.GetType().Name+" conservative queue measurement");
 }
 void BeginWire(ZNetPeer peer,Packet packet,string json,int lane){
  outgoingWires[lane]=new PendingWire{Packet=packet,Preparation=System.Threading.Tasks.Task.Run(()=>WireCompression.Encode(json)),Lane=lane};
 }
 void PumpWire(){
  var peer=ZNet.instance.GetServerPeer();if(peer==null||!peer.IsReady())return;
  for(int n=0;n<4;n++){
   int lane=(wireTurn+n)%4;if(!outgoingWires.TryGetValue(lane,out var wire))continue;
   if(!wire.Preparation.IsCompleted)continue;
   if(wire.Preparation.IsFaulted||wire.Preparation.IsCanceled){outgoingWires.Remove(lane);Warn("upload compression",new InvalidOperationException("Upload preparation failed; retained events will retry."));continue;}
   var prepared=wire.Preparation.Result;var bytes=prepared.Bytes;
   var packet=wire.Packet;
   if(((packet.Media!=null||packet.MediaChunk!=null)&&!shareProfile.Value)||((packet.Exploration!=null||packet.Pins!=null)&&!shareMap.Value)||(packet.Pins!=null&&!sharePins.Value)||(packet.Player!=null&&(packet.Player.ShareProfile!=shareProfile.Value||packet.Player.ShareMap!=shareMap.Value||packet.Player.SharePins!=sharePins.Value))){outgoingWires.Remove(lane);continue;}
   int length=Math.Min(WireTransfer.Chunk,bytes.Length-wire.Index*WireTransfer.Chunk);
   if(!telemetryBudget.Ready(lane,4096,Time.unscaledTime,UploadQueue(peer)))continue;
   var data=new byte[length];Array.Copy(bytes,wire.Index*WireTransfer.Chunk,data,0,length);
   var json=JsonConvert.SerializeObject(new WirePart{Id=wire.Id,Compressed=prepared.Compressed,Lane=lane,Index=wire.Index,Count=(bytes.Length+WireTransfer.Chunk-1)/WireTransfer.Chunk,Data=data});
   if(!telemetryBudget.TrySpend(lane,Encoding.UTF8.GetByteCount(json)+128,Time.unscaledTime,UploadQueue(peer)))continue;
   peer.m_rpc.Invoke(FragmentRpc,json);wire.Index++;wireTurn=(lane+1)%4;
   if(wire.Index*WireTransfer.Chunk>=bytes.Length)outgoingWires.Remove(lane);
   break;
  }
 }
 void ReceiveFragment(ZNetPeer peer,ZRpc rpc,string json){
  if(!ZNet.instance||!ZNet.instance.IsServer()||peer.m_rpc!=rpc||!peer.IsReady()||json==null||json.Length>4096)return;
  var now=Time.unscaledTime;if(!fragmentRates.TryGetValue(rpc,out var rate)||now-rate.start>10)rate=(now,0);
  rate.count++;if(fragmentRates.Count>=256&&!fragmentRates.ContainsKey(rpc))fragmentRates.Clear();fragmentRates[rpc]=rate;if(rate.count>80)return;
  try{var part=JsonConvert.DeserializeObject<WirePart>(json,new JsonSerializerSettings{MaxDepth=4,TypeNameHandling=TypeNameHandling.None});if(part==null)return;
   var bytes=wireReceiver.Accept(peer.m_uid.ToString(),part,Time.unscaledTime);if(bytes!=null)Receive(peer,rpc,Encoding.UTF8.GetString(bytes));
  }catch{Warn("fragment validation",new InvalidOperationException("Invalid telemetry fragment rejected."));}
 }
}
