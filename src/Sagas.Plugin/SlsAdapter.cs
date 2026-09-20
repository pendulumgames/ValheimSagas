using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
namespace ValheimSagas;
// Optional reflection adapter. Read SLS state on Unity's main thread; no SLS assembly reference.
internal static class SlsAdapter {
 const string Guid="MidnightsFX.StarLevelSystem";
 internal static bool Installed=>Chainloader.PluginInfos.ContainsKey(Guid);
 static readonly Type? ConfigType=AccessTools.TypeByName("StarLevelSystem.common.ValConfig");
 static readonly Type? DataType=AccessTools.TypeByName("StarLevelSystem.Data.ZoneScaleSystemData");
 static readonly Type? ColorType=AccessTools.TypeByName("StarLevelSystem.modules.Colorization");
 static readonly Dictionary<(Type,string),FieldInfo?> fields=new Dictionary<(Type,string),FieldInfo?>();
 static readonly Dictionary<Type,PropertyInfo?> values=new Dictionary<Type,PropertyInfo?>();
 static object? Field(Type? type,string name){if(type==null)return null;var key=(type,name);if(!fields.TryGetValue(key,out var field))fields[key]=field=AccessTools.Field(type,name);return field?.GetValue(null);}
 static object? Value(object? entry){if(entry==null)return null;var type=entry.GetType();if(!values.TryGetValue(type,out var property))values[type]=property=type.GetProperty("Value");return property?.GetValue(entry);}
 static T Config<T>(string name,T fallback){var entry=Field(ConfigType,name);return Value(entry) is T value?value:fallback;}
 internal static bool NemesisEnabled=>Installed&&Config("EnableNemesisSystem",false);
 internal static bool IsNemesisBoss(ZDO zdo)=>Installed&&zdo.GetBool("SLS_NEM_BOSS",false);
 internal static float? Score(ZDO? zdo){if(!NemesisEnabled||zdo==null||!zdo.GetFloat("SLS_NEM_SCORE",out var score)||float.IsNaN(score)||float.IsInfinity(score))return null;return score;}
 internal static SlsZoneCapture BeginCapture(){
  var state=new SlsWorldState{Installed=Installed};if(!state.Installed)return new SlsZoneCapture(state,null,null);
  state.NemesisEnabled=NemesisEnabled;state.ZoneScalingEnabled=Config("EnableZoneScalingBonus",false);state.OverlayEnabled=Config("EnableZoneMapOverlay",false);state.AboveFog=Config("ZoneOverlayAboveFog",false);state.Opacity=Mathf.Clamp01(Config("ZoneOverlayColorTransparency",.6f));
  state.OutlineInset=Minimap.instance?Minimap.instance.m_pixelSize:12f;
  if(!state.ZoneScalingEnabled||!state.OverlayEnabled||!(Field(DataType,"zonesBuilt") is bool built)||!built)return new SlsZoneCapture(state,null,null);
  var zones=Field(DataType,"Zones") as IEnumerable??throw new InvalidOperationException("SLS zone collection API unavailable.");
  var colors=new List<string>();if(Field(ColorType,"zoneOverlayColors") is IList palette)foreach(var value in palette)colors.Add(value is Color color?"#"+ColorUtility.ToHtmlStringRGB(color):"#808080");
  return new SlsZoneCapture(state,zones,colors.ToArray());
 }

}
public sealed partial class SagasPlugin {
 float nextSlsRefresh;
 SlsZoneCapture? slsCapture;Task<bool>? slsPublish;string slsWorld="";
 void RefreshSls(){
  if(service==null||!ZNet.instance||!ZNet.instance.IsServer()||slsWorld!=World){slsCapture?.Dispose();slsCapture=null;slsWorld=World;nextSlsRefresh=0;if(service==null||!ZNet.instance||!ZNet.instance.IsServer())return;}
  if(slsPublish!=null){if(!slsPublish.IsCompleted)return;if(slsPublish.IsFaulted||!slsPublish.Result)Warn("optional SLS publish",(Exception?)slsPublish.Exception??new InvalidOperationException("SLS storage queue rejected the update."));slsPublish=null;}
  if(slsCapture==null&&Time.unscaledTime<nextSlsRefresh)return;
  try{
   slsCapture??=SlsAdapter.BeginCapture();if(!slsCapture.Step())return;
   var snapshot=slsCapture.State;slsCapture.Dispose();slsCapture=null;nextSlsRefresh=Time.unscaledTime+15;
   // Only detached plain data crosses threads. Validation/JSON copying must not stall Unity.
   var target=service;var world=slsWorld;slsPublish=Task.Run(()=>target.UpdateSls(world,snapshot));
  }catch(Exception e){slsCapture?.Dispose();slsCapture=null;nextSlsRefresh=Time.unscaledTime+15;Warn("optional SLS integration",e);}
 }
 float? HostNemesisScore(long playerId){try{
  if(!SlsAdapter.NemesisEnabled||!ZNet.instance||!ZNet.instance.IsServer())return null;
  var local=Player.m_localPlayer;if(local&&local.GetPlayerID()==playerId)return SlsAdapter.Score(local.GetComponent<ZNetView>()?.GetZDO());
  if(ZDOMan.instance==null)return null;foreach(var peer in ZNet.instance.GetPeers())if(peer.IsReady()&&peer.m_playerID==playerId)return SlsAdapter.Score(ZDOMan.instance.GetZDO(peer.m_characterID));return null;
 }catch(Exception e){Warn("optional SLS score",e);return null;}}
}
