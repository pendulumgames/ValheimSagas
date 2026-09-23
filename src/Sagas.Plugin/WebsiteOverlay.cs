using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using Steamworks;
using HarmonyLib;
namespace ValheimSagas;

public sealed partial class SagasPlugin {
 ConfigEntry<KeyboardShortcut> websiteShortcut=null!;
 ConfigEntry<string> websiteUrl=null!,websiteOverride=null!;
 ConfigEntry<int> websitePort=null!;
 static readonly System.Reflection.FieldInfo directServerHost=AccessTools.Field(typeof(ZNet),"m_serverHost");
 string ListenAddress()=>WebsiteAddress.ListenPrefix(prefix.Value,websitePort.Value,ZNet.instance&&ZNet.instance.IsDedicated());
 string HostWebsiteAddress(){var custom=WebsiteAddress.Validate(websiteUrl.Value);if(custom!="")return custom;return WebsiteAddress.FromHost(serverAddress.Value,WebsiteAddress.ListenPort(ListenAddress()));}
 void LogWebsiteSetup(){
  if(!host.Value){Logger.LogInfo("Sagas website disabled by Server / EnableWebsite.");return;}
  var bind=ListenAddress();int port=WebsiteAddress.ListenPort(bind);
  if(service?.WebsiteListening!=true){Logger.LogWarning("Sagas website did not start. Check the earlier website-listener error, an available TCP web port, and host-panel allocation. Set WebsitePort, or correct the advanced ListenPrefix override.");return;}
  Logger.LogInfo("Sagas website listening on TCP port "+port+"; viewing is "+(requireToken.Value?"private (login required).":"public (shared data only)."));
  if(!string.IsNullOrWhiteSpace(prefix.Value))Logger.LogInfo("Advanced ListenPrefix is active and overrides WebsitePort. Clear ListenPrefix to use the simplified port setting.");
  var local=WebsiteAddress.Validate(bind);if(ZNet.instance.IsDedicated()&&local!=""&&new Uri(local).IsLoopback)Logger.LogWarning("The dedicated website is bound to loopback: remote browsers cannot connect directly. Clear ListenPrefix to listen on all interfaces using WebsitePort, or keep it if a local reverse proxy is intentional.");
  var url=HostWebsiteAddress();if(url!="")Logger.LogInfo("Sagas browser address: "+url);
  else if(!string.IsNullOrWhiteSpace(websiteUrl.Value))Logger.LogWarning("WebsiteUrl is invalid. Use a complete HTTP(S) browser URL without credentials, query or fragment.");
  else Logger.LogInfo("Sagas direct-IP clients can open the server address on web port "+port+". For relay/join-code connections or a custom domain, set Server / WebsiteUrl. The host panel/firewall must allow this TCP web port; Sagas does not open ports.");
 }
 const string WebsiteRequest="Sagas.Website.Request.V1",WebsiteResponse="Sagas.Website.Response.V1";
 string websiteNonce="",websiteWorld=""; float websiteDeadline,nextWebsiteOpen;
 readonly Dictionary<ZRpc,float> websiteRates=new Dictionary<ZRpc,float>();
 sealed class HiddenShortcutMigration { public bool Browsable=false; }
 void MigrateShortcutDefaults(){
  var done=Config.Bind("Internal","ModernShortcutDefaults",false,new ConfigDescription("One-time migration of the original conflicting Sagas shortcuts; custom shortcuts are preserved.",null,new HiddenShortcutMigration()));
  if(done.Value)return;
  foreach(var pair in new[]{(loginShortcut,KeyCode.F8,KeyCode.Insert),(revokeShortcut,KeyCode.F9,KeyCode.End),(websiteShortcut,KeyCode.F10,KeyCode.Home)})
   if(pair.Item1.Value.Equals(new KeyboardShortcut(pair.Item2,KeyCode.LeftControl)))pair.Item1.Value=new KeyboardShortcut(pair.Item3,KeyCode.LeftControl);
  done.Value=true;
 }
 void SetupWebsiteOverlay(){
  websiteShortcut=BindSetting("Website Login","OpenWebsite",new KeyboardShortcut(KeyCode.Home,KeyCode.LeftControl),"Open Sagas in the Steam overlay. Requires Steam overlay enabled. KeyCode.None disables this shortcut.");
  MigrateShortcutDefaults();
  websiteOverride=BindSetting("Website Login","WebsiteUrlOverride","","Optional personal HTTP(S) website URL, used instead of the host URL. No credentials in URLs.");
  websitePort=BindSetting("Server","WebsitePort",8877,new ConfigDescription("The TCP web port allocated by your host, for example 19908. Dedicated servers listen on all interfaces automatically; local hosts use loopback. The game port is separate. Advanced ListenPrefix overrides this setting.",new AcceptableValueRange<int>(1,65535)));
  websiteUrl=BindSetting("Server","WebsiteUrl","","Optional public HTTP(S) URL, such as https://sagas.example.com/ or http://74.112.78.28:19908/. Takes priority over automatic direct-join IP plus WebsitePort. Required when joining through a relay/join code without a usable direct IP, or using a proxy/external port mapping. Never include credentials. Does not change the listener port.");
 }
 void UpdateWebsiteOverlay(){
  if(websiteNonce!=""&&Time.unscaledTime>websiteDeadline){websiteNonce="";LoginNotice("Sagas website request timed out. Set WebsiteUrlOverride if the host has not updated.");}
  if(!Player.m_localPlayer||!ZNet.instance||!websiteShortcut.Value.IsDown()||Time.unscaledTime<nextWebsiteOpen)return;
  nextWebsiteOpen=Time.unscaledTime+3;
  if(!string.IsNullOrWhiteSpace(websiteOverride.Value)){OpenWebsite(websiteOverride.Value);return;}
  if(ZNet.instance.IsServer()){
   if(!host.Value){LoginNotice("The Sagas website is disabled. Enable Server / EnableWebsite and restart the hosted world.");return;}
   if(service==null){LoginNotice("The Sagas website is still starting. Try again shortly.");return;}
   if(!service.WebsiteListening){LoginNotice("The website listener did not start. Check the Sagas startup log and your web port settings.");return;}
   var url=websiteUrl.Value;
   if(string.IsNullOrWhiteSpace(url)&&host.Value)url=ListenAddress().Replace("http://*:","http://127.0.0.1:").Replace("http://+:","http://127.0.0.1:");
   OpenWebsite(url);return;
  }
  var peer=ZNet.instance.GetServerPeer();if(peer==null||!peer.IsReady())return;
  websiteNonce=Guid.NewGuid().ToString("N");websiteWorld=World;websiteDeadline=Time.unscaledTime+10;
  peer.m_rpc.Invoke(WebsiteRequest,websiteNonce);
 }
 void OpenWebsite(string value){
  var url=WebsiteAddress.Validate(value);if(url==""){LoginNotice("Set Server / WebsiteUrl on the host, or your personal WebsiteUrlOverride, to the Sagas HTTP(S) address.");return;}
  try{if(!SteamUtils.IsOverlayEnabled()){LoginNotice("Enable the Steam overlay for Valheim, then try again. You can also open Sagas in your browser.");return;}SteamFriends.ActivateGameOverlayToWebPage(url);}
  catch{LoginNotice("Steam overlay unavailable. Launch Valheim through Steam with the overlay enabled, or use your browser.");}
 }
 void RegisterWebsiteOverlay(ZNetPeer peer){
  peer.m_rpc.Register<string>(WebsiteRequest,(rpc,nonce)=>{
   if(!ZNet.instance||!ZNet.instance.IsServer()||rpc!=peer.m_rpc||!peer.IsReady()||nonce==null||nonce.Length!=32||!Guid.TryParseExact(nonce,"N",out _))return;
   if(websiteRates.TryGetValue(rpc,out var next)&&Time.unscaledTime<next)return;
   if(websiteRates.Count>256)websiteRates.Clear();websiteRates[rpc]=Time.unscaledTime+3;
   string result;if(!host.Value)result="!The host has disabled the Sagas website.";
   else if(service==null)result="!The host website is still starting. Try again shortly.";
   else if(!service.WebsiteListening)result="!The host website listener failed. Ask the host to check the Sagas startup log and allocated web port.";
   else if(!string.IsNullOrWhiteSpace(websiteUrl.Value)&&WebsiteAddress.Validate(websiteUrl.Value)=="")result="!The host WebsiteUrl is invalid. Use a complete HTTP(S) URL without credentials.";
   else {result=HostWebsiteAddress();if(result=="")result="@port="+WebsiteAddress.ListenPort(ListenAddress());}
   rpc.Invoke(WebsiteResponse,nonce+":"+result);
  });
  peer.m_rpc.Register<string>(WebsiteResponse,(rpc,response)=>{
   if(!ZNet.instance||ZNet.instance.IsServer()||ZNet.instance.GetServerPeer()?.m_rpc!=rpc||websiteNonce==""||Time.unscaledTime>websiteDeadline||World!=websiteWorld||!Player.m_localPlayer||response==null||response.Length>2081||!response.StartsWith(websiteNonce+":",StringComparison.Ordinal))return;
   websiteNonce="";var value=response.Substring(33);if(value.StartsWith("!",StringComparison.Ordinal)){LoginNotice(value.Substring(1));return;}
   if(value.StartsWith("@port=",StringComparison.Ordinal)){if(!int.TryParse(value.Substring(6),out var port))return;value=WebsiteAddress.FromHost(directServerHost?.GetValue(null) as string,port);if(value==""){LoginNotice("This connection does not expose a direct server IP. Ask the host to set Server / WebsiteUrl to the public website URL, or set your WebsiteUrlOverride.");return;}}
   OpenWebsite(value);
  });
 }
}
