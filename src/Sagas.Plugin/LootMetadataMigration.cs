using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
namespace ValheimSagas;
internal static class LootMetadataMigration {
 // Ground provenance belongs to this network object, never to the ItemData
 // copied into inventories, split stacks, equipment, or subsequent player drops.
 internal static string Read(ItemDrop item,string groundKey,string legacyKey,string fallback="") {
  var zdo=item.GetComponent<ZNetView>().GetZDO();
  if(zdo.GetString(LootTrackingMetadata.GroundSource,"")!="")return zdo.GetString(groundKey,fallback);
  return item.m_itemData.m_customData.TryGetValue(legacyKey,out var value)?value:fallback;
 }
 [HarmonyPatch(typeof(ItemDrop),nameof(ItemDrop.Load))]
 static class GroundLoad {
  static void Postfix(ItemDrop __instance) {
   var v=__instance.GetComponent<ZNetView>();if(!v||!v.IsValid()||!v.IsOwner())return;
   var data=__instance.m_itemData.m_customData;
   bool origin=data.TryGetValue(LootTrackingMetadata.LegacyOrigin,out var oldOrigin);
   bool source=data.TryGetValue(LootTrackingMetadata.LegacySource,out var oldSource);
   if(!origin&&!source)return;
   var zdo=v.GetZDO();
   if(zdo.GetString(LootTrackingMetadata.GroundSource,"")=="") {
    zdo.Set(LootTrackingMetadata.GroundOrigin,oldOrigin??"");
    zdo.Set(LootTrackingMetadata.GroundSource,oldSource??"legacy");
   }
   LootTrackingMetadata.RemoveLegacy(data);
   ItemDrop.SaveToZDO(__instance.m_itemData,zdo);
  }
 }
 [HarmonyPatch]
 static class InventoryInsert {
  // Includes old-save customData overloads and current ItemData overloads.
  // Run before InventorySlots' stacking prefixes, not after a rejected insert.
  static IEnumerable<MethodBase> TargetMethods()=>typeof(Inventory).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
   .Where(m=>(m.Name=="AddItem"||m.Name=="CanAddItem")&&m.GetParameters().Any(p=>p.ParameterType==typeof(ItemDrop.ItemData)||p.ParameterType==typeof(Dictionary<string,string>)));
  [HarmonyPrefix,HarmonyPriority(Priority.First),HarmonyBefore("sighsorry.InventorySlots")]
  static void Prefix(object[] __args) {
   foreach(var arg in __args) {
    if(arg is ItemDrop.ItemData item)LootTrackingMetadata.RemoveLegacy(item.m_customData);
    else if(arg is Dictionary<string,string> data)LootTrackingMetadata.RemoveLegacy(data);
   }
  }
 }
 [HarmonyPatch(typeof(Inventory),nameof(Inventory.Load),new[]{typeof(ZPackage)})]
 static class InventoryLoaded {
  [HarmonyPostfix,HarmonyPriority(Priority.Last)]
  static void Postfix(Inventory __instance) {
   foreach(var item in __instance.GetAllItems())LootTrackingMetadata.RemoveLegacy(item.m_customData);
  }
 }
}
