using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using Steamworks;
namespace ValheimSagas;

public sealed partial class SagasPlugin {
 ConfigEntry<KeyboardShortcut> websiteShortcut=null!;
 ConfigEntry<string> websiteUrl=null!,websiteOverride=null!;
 const string WebsiteRequest="Sagas.Website.Request.V1",WebsiteResponse="Sagas.Website.Response.V1";
 string websiteNonce="",websiteWorld=""; float websiteDeadline,nextWebsiteOpen;
 readonly Dictionary<ZRpc,float> websiteRates=new Dictionary<ZRpc,float>();
 void SetupWebsiteOverlay(){
  websiteShortcut=BindSetting("Website Login","OpenWebsite",new KeyboardShortcut(KeyCode.F10,KeyCode.LeftControl),"Open Sagas in the Steam overlay. Requires Steam overlay enabled. KeyCode.None disables this shortcut.");
  websiteOverride=BindSetting("Website Login","WebsiteUrlOverride","","Optional personal HTTP(S) website URL, used instead of the host URL. No credentials in URLs.");
  websiteUrl=BindSetting("Server","WebsiteUrl","","Public HTTP(S) Sagas website URL for the Steam shortcut, for example http://example.com:19908/. Use the web port, not the game port. Shared only when a player presses the shortcut; never include credentials. Required for remote clients.");
 }
 void UpdateWebsiteOverlay(){
  if(websiteNonce!=""&&Time.unscaledTime>websiteDeadline){websiteNonce="";LoginNotice("Sagas website request timed out. Set WebsiteUrlOverride if the host has not updated.");}
  if(!Player.m_localPlayer||!ZNet.instance||!websiteShortcut.Value.IsDown()||Time.unscaledTime<nextWebsiteOpen)return;
  nextWebsiteOpen=Time.unscaledTime+3;
  if(!string.IsNullOrWhiteSpace(websiteOverride.Value)){OpenWebsite(websiteOverride.Value);return;}
  if(ZNet.instance.IsServer()){
   var url=websiteUrl.Value;
   if(string.IsNullOrWhiteSpace(url)&&host.Value)url=prefix.Value.Replace("http://*:","http://127.0.0.1:").Replace("http://+:","http://127.0.0.1:");
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
   if(!ZNet.instance||!ZNet.instance.IsServer()||rpc!=peer.m_rpc||!peer.IsReady()||peer.m_playerID==0||nonce==null||nonce.Length!=32||!Guid.TryParseExact(nonce,"N",out _))return;
   if(websiteRates.TryGetValue(rpc,out var next)&&Time.unscaledTime<next)return;
   if(websiteRates.Count>256)websiteRates.Clear();websiteRates[rpc]=Time.unscaledTime+3;
   rpc.Invoke(WebsiteResponse,nonce+":"+WebsiteAddress.Validate(websiteUrl.Value));
  });
  peer.m_rpc.Register<string>(WebsiteResponse,(rpc,response)=>{
   if(!ZNet.instance||ZNet.instance.IsServer()||ZNet.instance.GetServerPeer()?.m_rpc!=rpc||websiteNonce==""||Time.unscaledTime>websiteDeadline||World!=websiteWorld||!Player.m_localPlayer||response==null||response.Length>2081||!response.StartsWith(websiteNonce+":",StringComparison.Ordinal))return;
   websiteNonce="";OpenWebsite(response.Substring(33));
  });
 }
}
