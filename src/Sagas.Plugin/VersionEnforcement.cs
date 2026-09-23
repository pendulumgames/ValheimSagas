using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace ValheimSagas;
public sealed partial class SagasPlugin {
 const string VersionHello="Sagas.Version.V1",VersionReply="Sagas.VersionReply.V1",VersionRejected="Sagas.VersionRejected.V1";
 ConfigEntry<bool> requireMatchingVersion=null!;
 readonly Dictionary<ZRpc,string> peerVersions=new Dictionary<ZRpc,string>();
 readonly HashSet<ZRpc> versionRejected=new HashSet<ZRpc>();
 string versionFailure="";
 void SetupVersionEnforcement(){requireMatchingVersion=BindSetting("Server","RequireMatchingVersion",true,"Require joining players to install this exact Valheim Sagas version. Missing or mismatched clients are rejected before joining the world. False permits mixed/missing clients, whose telemetry may be unavailable.");}
 void RegisterVersion(ZNet net,ZNetPeer peer){
  if(!net.IsServer())versionFailure="";
  peer.m_rpc.Register<string>(VersionHello,(rpc,version)=>{
   if(!net||!net.IsServer()||rpc!=peer.m_rpc||versionRejected.Contains(rpc))return;
   if(!VersionPolicy.Valid(version)){RejectVersion(net,peer,VersionPolicy.Rejection(Info.Metadata.Version.ToString(),null,true),false);return;}
   if(!peerVersions.ContainsKey(rpc)&&peerVersions.Count>=128){RejectVersion(net,peer,"Valheim Sagas connection limit reached. Try again shortly.",true);return;}
   // One declaration per connection; repeated handshakes cannot change validation.
   if(peerVersions.ContainsKey(rpc))return;
   peerVersions[rpc]=version;rpc.Invoke(VersionReply,Info.Metadata.Version.ToString()+"|"+(requireMatchingVersion.Value?"1":"0"));
  });
  peer.m_rpc.Register<string>(VersionReply,(rpc,reply)=>{
   if(!net||net.IsServer()||!peer.m_server||rpc!=peer.m_rpc||reply==null||reply.Length>64)return;
   var parts=reply.Split('|');if(parts.Length!=2||!VersionPolicy.Valid(parts[0])||(parts[1]!="0"&&parts[1]!="1"))return;
   if(parts[1]=="1"&&parts[0]!=Info.Metadata.Version.ToString())FailVersionClient(net,peer,"Valheim Sagas version mismatch. Server: "+parts[0]+"; your client: "+Info.Metadata.Version+". Install the same version as the server, then reconnect.");
  });
  peer.m_rpc.Register<string>(VersionRejected,(rpc,message)=>{if(!net||net.IsServer()||!peer.m_server||rpc!=peer.m_rpc||message==null||message.Length>500)return;FailVersionClient(net,peer,message.Replace("<","‹").Replace(">","›"));});
 }
 void FailVersionClient(ZNet net,ZNetPeer peer,string reason){if(!versionRejected.Add(peer.m_rpc))return;versionFailure=reason;Logger.LogWarning(reason);ZNet.SetExternalError(ZNet.ConnectionStatus.ErrorVersion);StartCoroutine(DisconnectVersionLater(net,peer));}
 bool CheckPeerVersion(ZNet net,ZRpc rpc){if(!net.IsServer())return versionFailure.Length==0;if(versionRejected.Contains(rpc))return false;peerVersions.TryGetValue(rpc,out var remote);string reason=VersionPolicy.Rejection(Info.Metadata.Version.ToString(),remote,requireMatchingVersion.Value);if(reason.Length==0)return true;var peer=net.GetPeers().Find(p=>p.m_rpc==rpc);if(peer!=null)RejectVersion(net,peer,reason,remote!=null);return false;}
 void RejectVersion(ZNet net,ZNetPeer peer,string reason,bool understands){if(!versionRejected.Add(peer.m_rpc))return;Logger.LogWarning(reason);if(understands)peer.m_rpc.Invoke(VersionRejected,reason);peer.m_rpc.Invoke("Error",(int)ZNet.ConnectionStatus.ErrorVersion);if(versionRejected.Count<=128)StartCoroutine(DisconnectVersionLater(net,peer));else net.Disconnect(peer);}
 IEnumerator DisconnectVersionLater(ZNet net,ZNetPeer peer){yield return new WaitForSecondsRealtime(1);if(net&&net.GetPeers().Contains(peer))net.Disconnect(peer);}
 [HarmonyPatch(typeof(ZNet),"OnNewConnection")]
 static class VersionConnectionPatch {[HarmonyPrefix, HarmonyPriority(Priority.First)]static void Prefix(ZNet __instance,ZNetPeer peer){Instance?.RegisterVersion(__instance,peer);}}
 [HarmonyPatch(typeof(ZNet),"RPC_ClientHandshake")]
 static class VersionClientHelloPatch {[HarmonyPrefix, HarmonyPriority(Priority.First)]static void Prefix(ZNet __instance,ZRpc rpc){if(Instance!=null&&!__instance.IsServer())rpc.Invoke(VersionHello,Instance.Info.Metadata.Version.ToString());}}
 [HarmonyPatch(typeof(ZNet),"RPC_PeerInfo")]
 static class VersionPeerInfoPatch {[HarmonyPrefix, HarmonyPriority(Priority.First)]static bool Prefix(ZNet __instance,ZRpc rpc)=>Instance==null||Instance.CheckPeerVersion(__instance,rpc);}
 [HarmonyPatch(typeof(ZNet),"Disconnect",new[]{typeof(ZNetPeer)})]
 static class VersionDisconnectPatch {[HarmonyPrefix]static void Prefix(ZNetPeer peer){Instance?.peerVersions.Remove(peer.m_rpc);Instance?.versionRejected.Remove(peer.m_rpc);}}
 [HarmonyPatch(typeof(ZNet),"Shutdown")]
 static class VersionShutdownPatch {[HarmonyPostfix]static void Postfix(){Instance?.peerVersions.Clear();Instance?.versionRejected.Clear();}}
 [HarmonyPatch(typeof(FejdStartup),"ShowConnectError")]
 static class VersionErrorPatch {[HarmonyPostfix, HarmonyPriority(Priority.Last)]static void Postfix(FejdStartup __instance){var plugin=Instance;if(plugin==null||plugin.versionFailure.Length==0||ZNet.GetConnectionStatus()!=ZNet.ConnectionStatus.ErrorVersion||!__instance.m_connectionFailedPanel.activeSelf)return;var label=AccessTools.Field(typeof(FejdStartup),"m_connectionFailedError")?.GetValue(__instance);if(label==null)return;var property=AccessTools.Property(label.GetType(),"text");var text=property?.GetValue(label) as string;if(text!=null&&!text.Contains(plugin.versionFailure))property!.SetValue(label,text+"\n\n"+plugin.versionFailure);}}
}
