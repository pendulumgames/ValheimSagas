using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ValheimSagas;
public sealed partial class SagasPlugin {
 ConfigEntry<KeyboardShortcut> loginShortcut=null!,revokeShortcut=null!;
 const string LoginRequest="Sagas.Login.Request.V1",LoginResponse="Sagas.Login.Response.V1";
 string loginNonce="",loginWorld="",loginPlayer="",clipboardCredential="";
 float loginUntil,clipboardUntil,nextLoginRequest;
 readonly HashSet<ZRpc> loginBusy=new HashSet<ZRpc>();
 readonly Dictionary<ZRpc,float> loginRates=new Dictionary<ZRpc,float>();
 float nextLoginFailure;
 void GuardLogin(Action action){
  try{action();}catch{
   // Clipboard/input support is optional. Never log exceptions that might contain
   // clipboard contents, and never let login failures interrupt telemetry or cleanup.
   if(Time.unscaledTime>=nextLoginFailure){nextLoginFailure=Time.unscaledTime+60;Logger.LogWarning("Sagas personal login input or clipboard is unavailable; telemetry continues.");}
  }
 }
 void ClearLoginClipboard(){if(clipboardCredential!=""&&GUIUtility.systemCopyBuffer==clipboardCredential)GUIUtility.systemCopyBuffer="";clipboardCredential="";}
 void SetupLogin(){
  loginShortcut=BindSetting("Website Login","CopyLoginToken",new KeyboardShortcut(KeyCode.Insert,KeyCode.LeftControl),"While playing, generate a personal website login and copy it to your clipboard. Replaces your previous token for this character/world. Never printed or saved locally.");
  revokeShortcut=BindSetting("Website Login","RevokeLoginToken",new KeyboardShortcut(KeyCode.End,KeyCode.LeftControl),"While playing, revoke your personal website login for this character/world.");
 }
 void LoginNotice(string message){Logger.LogInfo(message);if(Player.m_localPlayer)Player.m_localPlayer.Message(MessageHud.MessageType.Center,message);}
 void UpdateLogin(){
  if(clipboardCredential!=""&&Time.unscaledTime>=clipboardUntil){if(GUIUtility.systemCopyBuffer==clipboardCredential)GUIUtility.systemCopyBuffer="";clipboardCredential="";}
  if(loginNonce!=""&&Time.unscaledTime>=loginUntil){loginNonce="";LoginNotice("Sagas login request timed out. Try again shortly.");}
  if(!Player.m_localPlayer||!ZNet.instance||Player.m_localPlayer.GetPlayerID()==0)return;
  bool revoke=revokeShortcut.Value.IsDown();if(!revoke&&!loginShortcut.Value.IsDown())return;
  if(Time.unscaledTime<nextLoginRequest)return;nextLoginRequest=Time.unscaledTime+5;
  loginWorld=World;loginPlayer=Identity(Player.m_localPlayer.GetPlayerID());loginNonce=Guid.NewGuid().ToString("N");loginUntil=Time.unscaledTime+15;
  if(ZNet.instance.IsServer()){
   if(service==null){LoginNotice("Sagas is still starting. Try again shortly.");loginNonce="";return;}
   service.TouchPresence(World,loginPlayer,Player.m_localPlayer.GetPlayerName());service.Flush();
   try{if(revoke)service.Store.RevokePlayerLogin(World,loginPlayer);AcceptLogin(loginNonce,revoke?"":service.Store.RotatePlayerLogin(World,loginPlayer));}
   catch{loginNonce="";LoginNotice("Sagas could not update your login. Try again shortly.");}
  }else{
   var peer=ZNet.instance.GetServerPeer();if(peer==null||!peer.IsReady()){loginNonce="";return;}
   peer.m_rpc.Invoke(LoginRequest,loginNonce+(revoke?":revoke":":issue"));
   LoginNotice("Requesting personal Sagas login...");
  }
 }
 void RegisterLogin(ZNetPeer peer){
  RegisterWebsiteOverlay(peer);
  peer.m_rpc.Register<string>(LoginRequest,(rpc,request)=>{
   if(!ZNet.instance||!ZNet.instance.IsServer()||peer.m_rpc!=rpc||!peer.IsReady()||request==null||request.Length>40)return;
   var parts=request.Split(':');if(parts.Length!=2||!Guid.TryParseExact(parts[0],"N",out _)||(parts[1]!="issue"&&parts[1]!="revoke"))return;
   if(loginRates.TryGetValue(rpc,out var next)&&Time.unscaledTime<next)return;loginRates[rpc]=Time.unscaledTime+5;
   // Identity comes exclusively from the server's authenticated peer, never request JSON.
   if(service==null){rpc.Invoke(LoginResponse,parts[0]+":!service");return;}
   var playerId=PeerPlayerId(peer);if(playerId==0){rpc.Invoke(LoginResponse,parts[0]+":!identity");Logger.LogWarning("Sagas login delayed: owned player identity is not available yet.");return;}
   if(!loginBusy.Add(rpc)){rpc.Invoke(LoginResponse,parts[0]+":!busy");return;}
   var id=Identity(playerId);service.TouchPresence(World,id,peer.m_playerName);
   var loginService=service;var nonce=parts[0];bool revoke=parts[1]=="revoke";
   var worldForLogin=World;
   System.Threading.Tasks.Task.Run(()=>{
    string response;
    try{if(!loginService.Flush())throw new InvalidOperationException();if(revoke)loginService.Store.RevokePlayerLogin(worldForLogin, id);response=revoke?"":loginService.Store.RotatePlayerLogin(worldForLogin,id);}
    catch{response="!storage";}
    committed.Enqueue(()=>{loginBusy.Remove(rpc);if(ZNet.instance&&World==worldForLogin&&peer.IsReady()&&PeerPlayerId(peer)==playerId){rpc.Invoke(LoginResponse,nonce+":"+response);if(response=="!storage")Logger.LogWarning("Sagas login failed: credential storage unavailable.");}});
   });
  });
  peer.m_rpc.Register<string>(LoginResponse,(rpc,response)=>{
   if(!ZNet.instance||ZNet.instance.IsServer()||ZNet.instance.GetServerPeer()?.m_rpc!=rpc||response==null||response.Length>110)return;
   var split=response.IndexOf(':');if(split==32)AcceptLogin(response.Substring(0,split),response.Substring(split+1));
  });
 }
 void AcceptLogin(string nonce,string credential){
  if(nonce!=loginNonce||Time.unscaledTime>loginUntil||World!=loginWorld||!Player.m_localPlayer||Identity(Player.m_localPlayer.GetPlayerID())!=loginPlayer)return;
  loginNonce="";
  if(credential.StartsWith("!",StringComparison.Ordinal)){LoginNotice(credential=="!identity"?"Sagas is waiting for your character identity. Try again shortly.":credential=="!service"?"The server Sagas service is not ready.":credential=="!busy"?"Your previous Sagas login request is still processing. Try again shortly.":"The server could not save your Sagas login. Ask the host to check the server log.");return;}
  if(credential==""){if(clipboardCredential!=""&&GUIUtility.systemCopyBuffer==clipboardCredential)GUIUtility.systemCopyBuffer="";clipboardCredential="";LoginNotice("Sagas personal login revoked.");return;}
  if(credential.Length!=70||!credential.StartsWith("sagas_",StringComparison.Ordinal))return;
  GUIUtility.systemCopyBuffer=credential;clipboardCredential=credential;clipboardUntil=Time.unscaledTime+120;
  LoginNotice("Sagas login copied. Paste into website Login within 2 minutes. Previous token revoked.");
 }
}
