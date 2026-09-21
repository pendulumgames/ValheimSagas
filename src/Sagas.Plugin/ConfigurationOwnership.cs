using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace ValheimSagas;

public sealed partial class SagasPlugin {
 // Ownership is execution-side, not network synchronization. In particular, host
 // credentials and local filesystem paths must never become synchronized values.
 internal static bool HostSetting(string section)=>section=="Server"||section=="Lore";
 internal static bool RemoteSession=>ZNet.instance && !ZNet.instance.IsServer();
 sealed class ConfigurationManagerAttributes {
  readonly bool hostOwned;
  public ConfigurationManagerAttributes(bool hostOwned){this.hostOwned=hostOwned;}
  public bool ReadOnly=>hostOwned&&RemoteSession;
 }
 ConfigEntry<T> BindSetting<T>(string section,string name,T value,string description)=>BindSetting(section,name,value,new ConfigDescription(description));
 ConfigEntry<T> BindSetting<T>(string section,string name,T value,ConfigDescription description){
  var hostOwned=HostSetting(section);
  var scope=hostOwned?"Host only. This machine's setting is used only when it hosts a world. Remote server values are not copied here. Restart the hosted world after changing. ":"Client only. Your personal setting; the server cannot override it. ";
  var tags=description.Tags.Concat(new object[]{new ConfigurationManagerAttributes(hostOwned)}).ToArray();
  return Config.Bind(section,name,value,new ConfigDescription(scope+description.Description,description.AcceptableValues,tags));
 }
 static PropertyInfo? managerEntry;
 void SetupConfigurationManager(){
  if(!Chainloader.PluginInfos.TryGetValue("_shudnal.ConfigurationManager",out var plugin))return;
  try{
   var type=plugin.Instance.GetType().Assembly.GetType("ConfigurationManager.ConfigSettingEntry");
   managerEntry=type?.GetProperty("Entry",BindingFlags.Public|BindingFlags.Instance);
   var content=type?.GetMethod("GetSynchronizationContent",BindingFlags.Instance|BindingFlags.NonPublic);
   var set=type?.GetMethod("SetValue",BindingFlags.Instance|BindingFlags.NonPublic);
   if(managerEntry==null||content==null||set==null){Logger.LogWarning("Sagas config ownership labels unavailable for this ConfigurationManager version; ownership descriptions still apply.");return;}
   harmony!.Patch(content,postfix:new HarmonyMethod(typeof(SagasPlugin),nameof(ConfigOwnershipLabel)));
   harmony.Patch(set,prefix:new HarmonyMethod(typeof(SagasPlugin),nameof(ConfigOwnershipEdit)));
  }catch(Exception e){Warn("configuration ownership UI",e);}
 }
 static ConfigEntryBase? OwnManagerEntry(object instance){
  var entry=managerEntry?.GetValue(instance) as ConfigEntryBase;
  return entry!=null&&Instance!=null&&ReferenceEquals(entry.ConfigFile,Instance.Config)?entry:null;
 }
 static void ConfigOwnershipLabel(object __instance,ref GUIContent __result){
  var entry=OwnManagerEntry(__instance);if(entry==null)return;
  bool hostOwned=HostSetting(entry.Definition.Section);
  __result.text=hostOwned?"<color=#D4B374>S</color>":"<color=#88C8A8>C</color>";
  __result.tooltip=hostOwned?"Server / host-owned. Applied only on the machine hosting the world. Values and secrets are never synchronized. Locked while connected to another host. These are your local hosting settings, not a view of the remote server's config.":"Client-owned. Your privacy, notifications or login shortcut; never overridden by a host.";
 }
 static bool ConfigOwnershipEdit(object __instance){var entry=OwnManagerEntry(__instance);return entry==null||!HostSetting(entry.Definition.Section)||!RemoteSession;}
}
