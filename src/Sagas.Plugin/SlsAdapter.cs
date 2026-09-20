using System;
using System.Collections;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
namespace ValheimSagas;
// Optional reflection adapter. Read SLS state on Unity's main thread; no SLS assembly reference.
internal static class SlsAdapter {
 const string Guid="MidnightsFX.StarLevelSystem";
 internal static bool Installed=>Chainloader.PluginInfos.ContainsKey(Guid);
 static readonly Type? ConfigType=AccessTools.TypeByName("StarLevelSystem.common.ValConfig");
 static object? Field(Type? type,string name)=>type==null?null:AccessTools.Field(type,name)?.GetValue(null);
 static T Config<T>(string name,T fallback){var entry=Field(ConfigType,name);return entry?.GetType().GetProperty("Value")?.GetValue(entry) is T value?value:fallback;}
 internal static bool NemesisEnabled=>Installed&&Config("EnableNemesisSystem",false);
 internal static bool IsNemesisBoss(ZDO zdo)=>Installed&&zdo.GetBool("SLS_NEM_BOSS",false);
 internal static float? Score(ZDO? zdo){if(!NemesisEnabled||zdo==null||!zdo.GetFloat("SLS_NEM_SCORE",out var score)||float.IsNaN(score)||float.IsInfinity(score))return null;return score;}
 static float Number(object zone,string name)=>Convert.ToSingle(zone.GetType().GetProperty(name)?.GetValue(zone));
 internal static SlsWorldState Capture(){
  var state=new SlsWorldState{Installed=Installed};if(!state.Installed)return state;
  state.NemesisEnabled=NemesisEnabled;state.ZoneScalingEnabled=Config("EnableZoneScalingBonus",false);state.OverlayEnabled=Config("EnableZoneMapOverlay",false);state.AboveFog=Config("ZoneOverlayAboveFog",false);state.Opacity=Mathf.Clamp01(Config("ZoneOverlayColorTransparency",.6f));
  // Minimap isn't instantiated on dedicated servers; installed default is12m per map pixel.
  state.OutlineInset=Minimap.instance?Minimap.instance.m_pixelSize:12f;
  if(!state.ZoneScalingEnabled||!state.OverlayEnabled)return state;
  var data=AccessTools.TypeByName("StarLevelSystem.Data.ZoneScaleSystemData");if(!(Field(data,"zonesBuilt") is bool built)||!built)return state;
  var colors=Field(AccessTools.TypeByName("StarLevelSystem.modules.Colorization"),"zoneOverlayColors") as IList;
  var zones=Field(data,"Zones") as IEnumerable;if(zones==null)throw new InvalidOperationException("SLS zone collection API unavailable.");
  foreach(var zone in zones){if(state.Zones.Count>=40000)throw new InvalidOperationException("SLS zone count exceeds the supported 40000-zone snapshot bound.");var level=Convert.ToInt32(zone.GetType().GetProperty("ZoneLevel")?.GetValue(zone));var color=new Color(.5f,.5f,.5f);
   if(level>=1&&colors!=null&&colors.Count>0&&colors[(level-1)%colors.Count] is Color configured)color=configured;
   state.Zones.Add(new SlsZone{MinX=Number(zone,"MinX"),MaxX=Number(zone,"MaxX"),MinZ=Number(zone,"MinZ"),MaxZ=Number(zone,"MaxZ"),Level=level,Color="#"+ColorUtility.ToHtmlStringRGB(color)});
  }return state;
 }
}
public sealed partial class SagasPlugin {
 float nextSlsRefresh;
 void RefreshSls(){if(service==null||!ZNet.instance||!ZNet.instance.IsServer()||Time.unscaledTime<nextSlsRefresh)return;nextSlsRefresh=Time.unscaledTime+15;
  try{if(!service.UpdateSls(World,SlsAdapter.Capture()))throw new InvalidOperationException("SLS snapshot validation or storage queue rejected the update.");}
  catch(Exception e){service.UpdateSls(World,new SlsWorldState{Installed=SlsAdapter.Installed,NemesisEnabled=SlsAdapter.NemesisEnabled});Warn("optional SLS integration",e);}
 }
 float? HostNemesisScore(long playerId){try{
  if(!SlsAdapter.NemesisEnabled||!ZNet.instance||!ZNet.instance.IsServer())return null;
  var local=Player.m_localPlayer;if(local&&local.GetPlayerID()==playerId)return SlsAdapter.Score(local.GetComponent<ZNetView>()?.GetZDO());
  if(ZDOMan.instance==null)return null;foreach(var peer in ZNet.instance.GetPeers())if(peer.IsReady()&&peer.m_playerID==playerId)return SlsAdapter.Score(ZDOMan.instance.GetZDO(peer.m_characterID));return null;
 }catch(Exception e){Warn("optional SLS score",e);return null;}}
}
