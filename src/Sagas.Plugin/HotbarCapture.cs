using System;
using System.Collections.Generic;
using System.Linq;
namespace ValheimSagas;
internal static class HotbarCapture {
 // Mirrors installed HotkeyBar.UpdateIcons/GetBoundItems, but exports only numbered slots 1–8.
 // Caller owns ShareProfile consent; this must never be called to export the full inventory.
 internal static List<GearItem> Read(Player player,Func<ItemDrop.ItemData,string> iconCapture){
  var bound=new List<ItemDrop.ItemData>();player.GetInventory().GetBoundItems(bound);
  var result=new List<GearItem>(8);
  foreach(var item in bound.Where(i=>i!=null&&i.m_gridPos.y==0&&i.m_gridPos.x>=0&&i.m_gridPos.x<8).GroupBy(i=>i.m_gridPos.x).OrderBy(g=>g.Key).Select(g=>g.First())){
   var gear=Gear.Read(item);gear.HotbarSlot=item.m_gridPos.x+1;
   // Selected hands include shields/torches; selected ammunition has its own game field.
   EquippedState.Apply(gear,item,player);
   try { gear.IconId=iconCapture(item); JewelcraftingAdapter.Icons(gear,iconCapture); } catch { /* Optional artwork cannot suppress hotbar facts. */ }
   result.Add(gear);
  }
  return result;
 }
}
