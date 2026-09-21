using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
namespace ValheimSagas;

// Optional, read-only adapter for installed Jewelcrafting 2.0.10. Never loads the
// mod DLL itself or scans assemblies, and never opens item-container inventories.
internal static class JewelcraftingAdapter {
 const string Guid="org.bepinex.plugins.jewelcrafting";
 static readonly Type? Api=OptionalTypes.InPlugin(Guid,"Jewelcrafting.API");
 static readonly MethodInfo? GetGems=Api?.GetMethod("GetGems",new[]{typeof(ItemDrop.ItemData)}),GetColor=Api?.GetMethod("GetSocketableItemColor",new[]{typeof(ItemDrop.ItemData)}),GetJewelry=Api?.GetMethod("GetEquippedJewelry",new[]{typeof(Player)});
 static readonly Type? Gem=Api?.GetNestedType("GemInfo"),Visual=OptionalTypes.InPlugin(Guid,"Jewelcrafting.Visual");
 static readonly FieldInfo? Prefab=Gem?.GetField("gemPrefab"),Ranges=Gem?.GetField("gemEffectsPowerRange"),Finger=Visual?.GetField("equippedFingerItem"),Neck=Visual?.GetField("equippedNeckItem");
 static readonly FieldInfo? ArmorJewelry=OptionalTypes.InPlugin(Guid,"Jewelcrafting.JewelrySetup")?.GetField("upgradeableJewelry");
 internal static bool HasArmor(ItemDrop.ItemData item){try{return ArmorJewelry?.GetValue(null) is IEnumerable<string> names&&names.Contains(item.m_shared.m_name);}catch{Failed();return false;}}
 static float nextWarning;
 static void Failed(){if(Time.unscaledTime<nextWarning)return;nextWarning=Time.unscaledTime+60;Debug.LogWarning("Sagas: optional Jewelcrafting metadata unavailable; ordinary equipment tracking continues. Check installed mod compatibility.");}
 static string Clean(string? value,int max){var s=Regex.Replace(value??"","<[^>]*>","");s=new string(s.Select(c=>char.IsControl(c)?' ':c).ToArray());return s.Length<=max?s:s.Substring(0,max);}
 internal static void Read(ItemDrop.ItemData item,out List<GemSocket> sockets,out string color){
  sockets=new List<GemSocket>();color="";if(Api==null)return;
  if(GetGems==null||GetColor==null||Prefab==null||Ranges==null){Failed();return;}
  try{
   // A color exists only for Sockets. GetGems also supports boxes/bags: do not
   // invoke it for those, since their contents are not shared equipment.
   if(!(GetColor?.Invoke(null,new object[]{item}) is Color shade))return;
   color="#"+ColorUtility.ToHtmlStringRGB(shade);
   if(!(GetGems?.Invoke(null,new object[]{item}) is IEnumerable gems))return;
   foreach(var gem in gems){if(sockets.Count>=11)break;var socket=new GemSocket();sockets.Add(socket);if(gem==null)continue;
    socket.Prefab=Clean(Prefab?.GetValue(gem) as string,200);
    var prefab=ObjectDB.instance?ObjectDB.instance.GetItemPrefab(socket.Prefab):null;
    var data=prefab?prefab!.GetComponent<ItemDrop>()?.m_itemData:null;
    socket.Name=Clean(data!=null?SagasPlugin.Localize(data.m_shared.m_name):socket.Prefab,200);
    if(Ranges?.GetValue(gem) is IDictionary ranges)foreach(DictionaryEntry entry in ranges){
     if(socket.Effects.Count>=8)break;if(!(entry.Value is float[] powers)||powers.Length<2||powers.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))continue;
     // Public API midpoint is NOT the seeded roll. Preserve configured range,
     // without invented units, percentages or additive effective totals.
     var lo=powers[0].ToString("0.##",CultureInfo.InvariantCulture);var hi=powers[1].ToString("0.##",CultureInfo.InvariantCulture);
     socket.Effects.Add(Clean(entry.Key?.ToString(),160)+": "+lo+(lo==hi?" (configured power)":"–"+hi+" (configured range)"));
    }
   }
  }catch{Failed();}
 }
 static (ItemDrop.ItemData? finger,ItemDrop.ItemData? neck) Jewelry(Player player){
  if(GetJewelry==null)return (null,null);
  try{var visual=GetJewelry.Invoke(null,new object[]{player});return visual==null?(null,null):(Finger?.GetValue(visual) as ItemDrop.ItemData,Neck?.GetValue(visual) as ItemDrop.ItemData);}catch{Failed();return (null,null);}
 }
 internal static IEnumerable<ItemDrop.ItemData> Equipped(Player player){
  var items=player.GetInventory().GetEquippedItems();var jewelry=Jewelry(player);
  if(jewelry.finger!=null&&!items.Contains(jewelry.finger))items.Add(jewelry.finger);
  if(jewelry.neck!=null&&!items.Contains(jewelry.neck))items.Add(jewelry.neck);
  return items;
 }
 internal static void JewelrySlot(GearItem gear,ItemDrop.ItemData item,Player player){
  if(GetJewelry==null)return;var jewelry=Jewelry(player);
  if(ReferenceEquals(item,jewelry.finger)){gear.Slot="Ring";gear.Equipped=true;}
  else if(ReferenceEquals(item,jewelry.neck)){gear.Slot="Necklace";gear.Equipped=true;}
 }
 internal static void Icons(GearItem gear,Func<ItemDrop.ItemData,string> capture){
  foreach(var socket in gear.Sockets){if(socket.Prefab==""||!ObjectDB.instance)continue;var prefab=ObjectDB.instance.GetItemPrefab(socket.Prefab);var item=prefab?prefab!.GetComponent<ItemDrop>()?.m_itemData:null;if(item!=null)socket.IconId=capture(item);}
 }
}
