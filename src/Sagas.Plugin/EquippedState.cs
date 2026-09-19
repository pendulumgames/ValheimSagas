using System;
namespace ValheimSagas;
internal static class EquippedState {
 // Installed Humanoid exposes both held items publicly. GetCurrentWeapon omits shields and ammo.
 internal static bool IsActive(Player player,ItemDrop.ItemData item)=>item!=null&&
  (ReferenceEquals(item,player.RightItem)||ReferenceEquals(item,player.LeftItem)||ReferenceEquals(item,player.GetAmmoItem()));
 internal static void Apply(GearItem gear,ItemDrop.ItemData item,Player player){gear.Equipped=item.m_equipped;gear.Active=IsActive(player,item);}
}
