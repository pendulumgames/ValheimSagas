using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ValheimSagas;

// Optional read-only integration; Epic Loot remains responsible for its state.
internal static class EpicProgress {
 internal static bool Installed=>BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("randyknapp.mods.epicloot");
 internal static long CarriedGold(Player p)=>p.GetInventory().GetAllItems().Where(i=>i.m_dropPrefab&&i.m_dropPrefab.name=="Coins").Sum(i=>(long)Math.Max(0,i.m_stack));
 static object? Field(object instance,string name)=>AccessTools.Field(instance.GetType(),name)?.GetValue(instance);
 static string Short(string value,int length)=>value.Length>length?value.Substring(0,length):value;
 internal static string CompletionId(string world,string bounty){using var sha=SHA256.Create();return "bounty:"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(world+":"+bounty))).Replace("-","").ToLowerInvariant();}

 [HarmonyPatch]
 internal static class CompletedBounty {
  static MethodBase? Find(){var feature=AccessTools.TypeByName("EpicLoot.Adventure.Feature.BountiesAdventureFeature");var data=AccessTools.TypeByName("EpicLoot.Adventure.AdventureSaveData");return feature!=null&&data!=null?AccessTools.Method(feature,"OnBountyTargetSlain",new[]{data,typeof(string),typeof(string),typeof(bool)}):null;}
  static bool Prepare(){bool supported=Find()!=null;if(!supported&&Installed)Debug.LogWarning("Sagas Epic Loot bounty tracking unavailable for this installed API; completion capture is disabled.");return supported;}
  static MethodBase TargetMethod()=>Find()!;
  // Capture the existing record, never call GetAdventureSaveData (which may
  // create/mutate an Epic component). This method is the installed transition
  // after the main target and every required add have been slain.
  static void Prefix(object saveData,string bountyID,out object? __state){
   __state=null;try{var lookup=saveData?.GetType().GetMethod("GetBountyInfoByID",new[]{typeof(string)});var bounty=lookup?.Invoke(saveData,new object[]{bountyID});if(bounty!=null&&Field(bounty,"State")?.ToString()=="InProgress")__state=bounty;}catch(Exception e){Debug.LogWarning("Sagas bounty observation: "+e.GetType().Name);}
  }
  static void Postfix(object? __state){
   try{
    var p=Player.m_localPlayer;if(__state==null||!p||!ZNet.instance||Field(__state,"State")?.ToString()!="Complete"||Convert.ToInt64(Field(__state,"PlayerID"))!=p.GetPlayerID())return;
    var bountyId=AccessTools.Property(__state.GetType(),"ID")?.GetValue(__state) as string;if(string.IsNullOrEmpty(bountyId))return;
    var target=Field(__state,"Target");var prefab=target!=null?Field(target,"MonsterID")?.ToString()??"":"";
    var name=Field(__state,"TargetName")?.ToString()??"";if(name=="")name=prefab;
    SagasPlugin.Instance?.Record(new SagaEvent{Id=CompletionId(SagasPlugin.World,bountyId!),Kind="bounty",PlayerId=SagasPlugin.Identity(p.GetPlayerID()),PlayerName=p.GetPlayerName(),Prefab=Short(prefab,200),Name=Short(SagasPlugin.Localize(name),200),Source="epic-bounty-complete",Amount=1});
   }catch(Exception e){Debug.LogWarning("Sagas bounty completion: "+e.GetType().Name);}
  }
 }
}
