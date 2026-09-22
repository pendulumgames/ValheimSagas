using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
namespace ValheimSagas;
internal static class Gear {
 // Resolve optional types once, including the absent-mod case. Never scan assemblies per item.
 static readonly Type? ExtensionsType=OptionalTypes.InPlugin("randyknapp.mods.epicloot","EpicLoot.ItemDataExtensions");
 static readonly Type? EpicType=OptionalTypes.InPlugin("randyknapp.mods.epicloot","EpicLoot.EpicLoot");
 static readonly MethodInfo? magicGetter=ExtensionsType?.GetMethod("GetMagicItem",new[]{typeof(ItemDrop.ItemData)});
 sealed class MagicApi {
  internal FieldInfo? Rarity,Effects;internal MethodInfo? GetEffects,Text,Color;
  internal MagicApi(Type t){Rarity=AccessTools.Field(t,"Rarity");Effects=AccessTools.Field(t,"Effects");GetEffects=t.GetMethod("GetEffects",new[]{typeof(string),typeof(bool)});Text=t.GetMethods().FirstOrDefault(m=>m.Name=="GetEffectText"&&m.GetParameters().Length==4);if(Rarity!=null)Color=EpicType?.GetMethod("GetRarityColor",new[]{Rarity.FieldType});}
 }
 static readonly Dictionary<Type,MagicApi> magicApis=new Dictionary<Type,MagicApi>();
 static readonly Dictionary<Type,(PropertyInfo? Type,FieldInfo? Value)> effectApis=new Dictionary<Type,(PropertyInfo?,FieldInfo?)>();
 static readonly FieldInfo[] damageFields=typeof(HitData.DamageTypes).GetFields(BindingFlags.Public|BindingFlags.Instance).Where(f=>f.FieldType==typeof(float)).ToArray();
 internal static void Magic(ItemDrop.ItemData item,out string rarity,out string rarityColor,out List<string> effects) {
  rarity="";rarityColor="";effects=new List<string>();
  try {
   var magic=magicGetter?.Invoke(null,new object[]{item});if(magic==null)return;
   var type=magic.GetType();if(!magicApis.TryGetValue(type,out var api))magicApis[type]=api=new MagicApi(type);
   var rarityValue=api.Rarity?.GetValue(magic);rarity=rarityValue?.ToString()??"";
   var all=api.GetEffects?.Invoke(magic,new object?[]{null,true}) as IEnumerable ?? api.Effects?.GetValue(magic) as IEnumerable;
   var textMethod=api.Text;
   try { rarityColor=RarityColors.Normalize(api.Color?.Invoke(null,new[]{rarityValue}) as string); }
   catch { /* Optional palette failures must not hide effects or item data. */ }
   if(all!=null)foreach(var effect in all){
    var effectType=effect.GetType();if(!effectApis.TryGetValue(effectType,out var effectApi))effectApis[effectType]=effectApi=(AccessTools.Property(effectType,"EffectType"),AccessTools.Field(effectType,"EffectValue"));
    string value=effectApi.Type?.GetValue(effect)+": "+effectApi.Value?.GetValue(effect);
    if(textMethod!=null)value=textMethod.Invoke(null,new[]{effect,rarityValue,(object)false,null})?.ToString()??value;
    value=Regex.Replace(SagasPlugin.Localize(value),"<[^>]*>","");value=new string(value.Select(c=>char.IsControl(c)?' ':c).ToArray());if(effects.Count<64)effects.Add(value.Length>500?value.Substring(0,500):value);
   }
  }catch{effects.Add("Epic Loot metadata unavailable for this installed item; conditional totals are not inferred.");}
 }
 internal static GearItem Read(ItemDrop.ItemData i) {
  Magic(i,out var rarity,out var rarityColor,out var effects);
  var g=new GearItem{Slot=i.m_shared.m_itemType.ToString(),Name=SagasPlugin.Localize(i.m_shared.m_name),Prefab=i.m_dropPrefab?i.m_dropPrefab.name:"",Type=i.m_shared.m_itemType.ToString(),Quality=i.m_quality,Rarity=rarity,RarityColor=rarityColor,Effects=effects,Durability=i.m_durability,MaxDurability=i.GetMaxDurability(),Note="Item values use installed game/mod APIs; base uses quality 1. Conditional/set/socket effects are descriptive, not added into totals. Icons are shared runtime captures when available."};
  if(new[]{"Helmet","Chest","Legs","Shoulder","Hands"}.Contains(g.Type)||JewelcraftingAdapter.HasArmor(i)){g.Stats["Armor"]=i.GetArmor();g.BaseStats["Armor"]=i.GetArmor(1,i.m_worldLevel);} if(i.IsWeapon()||g.Type=="Shield"){g.Stats["Block (zero skill)"]=i.GetBlockPower(0);g.BaseStats["Block (zero skill)"]=i.GetBlockPower(1,0);}
  g.Stats["Movement modifier"]=i.m_shared.m_movementModifier;Damage(g.Stats,i.GetDamage());Damage(g.BaseStats,i.GetDamage(1,i.m_worldLevel));
  foreach(var mod in i.m_shared.m_damageModifiers)g.Effects.Add(mod.m_type+": "+mod.m_modifier);
  if(i.m_shared.m_setStatusEffect)g.Effects.Add("Set "+i.m_shared.m_setName+" ("+i.m_shared.m_setSize+" pieces): "+SagasPlugin.Localize(i.m_shared.m_setStatusEffect.m_name));
  JewelcraftingAdapter.Read(i,out var sockets,out var socketColor);g.Sockets=sockets;g.SocketColor=socketColor;
  return g;
 }
 static void Damage(Dictionary<string,float> values,HitData.DamageTypes d){ foreach(var f in damageFields){var n=(float)f.GetValue(d);if(n!=0)values["Damage "+f.Name.Replace("m_","")]=n;} }
}
internal static class Hooks {
 const string Origin=LootTrackingMetadata.GroundOrigin, Source=LootTrackingMetadata.GroundSource;
 [ThreadStatic] internal static int CreatureDropDepth;
 internal sealed class Credit {internal string Id="",Name="";}
 [ThreadStatic] static Credit? currentCredit;
 static readonly ConditionalWeakTable<object,Credit> iteratorCredits=new ConditionalWeakTable<object,Credit>();
 internal static readonly Dictionary<int,(string source,Credit? credit)> Spawned=new Dictionary<int,(string,Credit?)>();
 internal static SagaEvent ItemEvent(ItemDrop item,string kind) {
  var i=item.m_itemData; var pos=item.transform.position;var view=item.GetComponent<ZNetView>();
  Gear.Magic(i,out var rarity,out var rarityColor,out var effects);
  JewelcraftingAdapter.Read(i,out var sockets,out var socketColor);
  var zdo=view.GetZDO();
  return new SagaEvent{Sockets=sockets,SocketColor=socketColor,Id=SagasPlugin.EventId(kind,zdo.m_uid),Kind=kind,ItemType=i.m_shared.m_itemType.ToString(),Prefab=i.m_dropPrefab?i.m_dropPrefab.name:item.name.Replace("(Clone)",""),Name=SagasPlugin.Localize(i.m_shared.m_name),Amount=i.m_stack,Quality=i.m_quality,Rarity=rarity,RarityColor=rarityColor,Effects=effects,X=pos.x,Z=pos.z,Biome=WorldGenerator.instance!=null?WorldGenerator.instance.GetBiome(pos).ToString():"Unknown",Provenance=LootMetadataMigration.Read(item,Origin,LootTrackingMetadata.LegacyOrigin),Source=LootMetadataMigration.Read(item,Source,LootTrackingMetadata.LegacySource,"unknown")};
 }
 [HarmonyPatch]
 static class Death {
  static IEnumerable<MethodBase> TargetMethods(){yield return AccessTools.Method(typeof(Character),nameof(Character.OnDeath));yield return AccessTools.Method(typeof(Player),nameof(Player.OnDeath));}
  [HarmonyPrefix,HarmonyPriority(Priority.First)]static void Prefix(Character __instance){
   try {var c=__instance;var view=c.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner()||c.GetHealth()>0)return;
    var hit=AccessTools.Field(typeof(Character),"m_lastHit").GetValue(c) as HitData;var killer=hit?.GetAttacker() as Player;var victim=c as Player;var pos=c.transform.position;
    // m_lastHit is actual final damage: unattributed DOT/environment/pets stay unknown.
    SagasPlugin.Instance?.Record(new SagaEvent{Id=SagasPlugin.EventId("death",view.GetZDO().m_uid),Kind=victim?"death":"kill",PlayerId=victim?SagasPlugin.Identity(victim.GetPlayerID()):killer?SagasPlugin.Identity(killer.GetPlayerID()):view.GetZDO().GetString("sagas.lastAttacker",""),PlayerName=victim?victim.GetPlayerName():killer?killer.GetPlayerName():view.GetZDO().GetString("sagas.lastName","Unattributed"),Name=SagasPlugin.Localize(c.m_name),Prefab=c.name.Replace("(Clone)",""),Stars=Math.Max(0,c.GetLevel()-1),Boss=c.IsBoss(),NemesisBoss=!victim&&SlsAdapter.IsNemesisBoss(view.GetZDO()),DurationSeconds=c.IsBoss()&&ZNet.instance?BossTiming.Elapsed(view.GetZDO().GetLong("sagas.bossStartMs",0),(long)(ZNet.instance.GetTimeSeconds()*1000)):null,X=pos.x,Z=pos.z,Biome=WorldGenerator.instance!=null?WorldGenerator.instance.GetBiome(pos).ToString():"Unknown",Source=killer||view.GetZDO().GetString("sagas.lastAttacker","")!=""?"finishing-blow":"unattributed",Contributors=view.GetZDO().GetString("sagas.contributors","").Split(new[]{(char)44},StringSplitOptions.RemoveEmptyEntries).ToList()});
   }catch(Exception e){Debug.LogWarning("Sagas death capture: "+e.Message);}
  }
 }
 [HarmonyPatch(typeof(Character),nameof(Character.ApplyDamage))]
 static class DamageCredit {
  static void Prefix(Character __instance,out float __state){__state=__instance.GetHealth();}
  static void Postfix(Character __instance,HitData hit,float __state){try{var view=__instance.GetComponent<ZNetView>();if(!view||!view.IsValid()||!view.IsOwner()||__instance.GetHealth()>=__state)return;var p=hit.GetAttacker() as Player;var zdo=view.GetZDO();if(__instance.IsBoss()&&ZNet.instance){long prior=zdo.GetLong("sagas.bossStartMs",0),now=(long)(ZNet.instance.GetTimeSeconds()*1000),started=BossTiming.ObserveDamage(prior,__state,__instance.GetMaxHealth(),now);if(started!=prior){zdo.Set("sagas.bossStartMs",started);if(started>0)zdo.Set("sagas.contributors","");}}var id=p?SagasPlugin.Identity(p.GetPlayerID()):"";zdo.Set("sagas.lastAttacker",id);zdo.Set("sagas.lastName",p?p.GetPlayerName():"Unattributed");if(id!=""){var people=new HashSet<string>(zdo.GetString("sagas.contributors","").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries));if(people.Count<64){people.Add(id);zdo.Set("sagas.contributors",string.Join(",",people));}}}catch{}}
 }
 [HarmonyPatch(typeof(CharacterDrop),"OnDeath")]
 static class CreatureCredit {
  [HarmonyPrefix,HarmonyPriority(Priority.First)] static void Prefix(CharacterDrop __instance,out Credit? __state){__state=currentCredit;var c=__instance.GetComponent<Character>();var v=c?c.GetComponent<ZNetView>():null;currentCredit=v&&v.IsValid()?new Credit{Id=v.GetZDO().GetString("sagas.lastAttacker",""),Name=v.GetZDO().GetString("sagas.lastName","")}:null;}
  static Exception? Finalizer(Credit? __state,Exception? __exception){currentCredit=__state;return __exception;}
 }
 [HarmonyPatch]
 static class SlsIteratorCredit {
  static bool Prepare()=>AccessTools.TypeByName("StarLevelSystem.modules.Loot.LootPerformanceChanges")!=null;
  static MethodBase TargetMethod()=>AccessTools.Method(AccessTools.TypeByName("StarLevelSystem.modules.Loot.LootPerformanceChanges"),"DropItemsAsync");
  static void Postfix(object __result){if(__result!=null&&currentCredit!=null)iteratorCredits.Add(__result,currentCredit);}
 }
 [HarmonyPatch(typeof(CharacterDrop),nameof(CharacterDrop.DropItems))]
 static class DropScope {
  [HarmonyPrefix,HarmonyPriority(Priority.First)]static void Prefix(){CreatureDropDepth++;}
  [HarmonyFinalizer]static Exception? Finalizer(Exception? __exception){CreatureDropDepth=Math.Max(0,CreatureDropDepth-1);return __exception;}
 }
 [HarmonyPatch(typeof(ItemDrop),"Awake")]
 static class ItemAwake {static void Postfix(ItemDrop __instance){if(CreatureDropDepth>0)Spawned[__instance.GetInstanceID()]=("creature",currentCredit);}}
 [HarmonyPatch(typeof(ItemDrop),"OnDestroy")]
 static class ItemDestroyed {static void Prefix(ItemDrop __instance){Spawned.Remove(__instance.GetInstanceID());}}
 [HarmonyPatch(typeof(ItemDrop),"Start")]
 static class ItemStart { [HarmonyPostfix,HarmonyPriority(Priority.Last)]static void Postfix(ItemDrop __instance){
  try {if(!Spawned.TryGetValue(__instance.GetInstanceID(),out var originSource))return;Spawned.Remove(__instance.GetInstanceID());var v=__instance.GetComponent<ZNetView>();if(!v||!v.IsValid()||!v.IsOwner())return;
   var zdo=v.GetZDO();if(zdo.GetString(Source,"")!="")return;
   zdo.Set(Origin,SagasPlugin.EventId("drop",zdo.m_uid));zdo.Set(Source,originSource.source);var dropEvent=ItemEvent(__instance,"drop");dropEvent.PlayerId=originSource.credit?.Id??"";dropEvent.PlayerName=originSource.credit?.Name??"";SagasPlugin.Instance?.Record(dropEvent);
  }catch(Exception e){Debug.LogWarning("Sagas drop capture: "+e.Message);}
 }}
 [HarmonyPatch(typeof(Humanoid),nameof(Humanoid.Pickup))]
 static class Pickup {
  static void Prefix(Humanoid __instance,GameObject go,out SagaEvent? __state){
   __state=null;try{if(__instance!=Player.m_localPlayer)return;var item=go.GetComponent<ItemDrop>();if(!item)return;item.Load();var v=item.GetComponent<ZNetView>();if(!v||!v.IsValid())return;
    var e=ItemEvent(item,LootMetadataMigration.Read(item,Origin,LootTrackingMetadata.LegacyOrigin)!=""?"collect":"pickup");e.PlayerId=SagasPlugin.Identity(Player.m_localPlayer.GetPlayerID());e.PlayerName=Player.m_localPlayer.GetPlayerName();__state=e;
   }catch{__state=null;}
  }
  static void Postfix(bool __result,SagaEvent? __state){if(__result&&__state!=null)SagasPlugin.Instance?.Record(__state);}
 }
 [HarmonyPatch(typeof(ItemDrop),"AutoStackItems")]
 static class Merge {
  static void Prefix(ItemDrop __instance,out int __state){__state=__instance.m_itemData.m_stack;}
  static void Postfix(ItemDrop __instance,int __state){if(__instance.m_itemData.m_stack!=__state){var v=__instance.GetComponent<ZNetView>();if(!v||!v.IsValid()||!v.IsOwner())return;v.GetZDO().Set(Origin,"");v.GetZDO().Set(Source,"mixed-ground-stack");}}
 }
 [HarmonyPatch]
 static class EpicSpawn {
  static bool Prepare()=>AccessTools.TypeByName("EpicLoot.LootRoller")!=null;
  static MethodBase TargetMethod()=>AccessTools.Method(AccessTools.TypeByName("EpicLoot.LootRoller"),"SpawnLootForDrop");
  static void Postfix(GameObject __result,bool initializeObject){if(initializeObject&&__result){var i=__result.GetComponent<ItemDrop>();if(i)Spawned[i.GetInstanceID()]=("generated",currentCredit);}}
 }
 // Jewelcrafting equipment is instantiated after the vanilla drop list. Mark
 // the actual spawned object; ItemStart reads sockets after generation finishes.
 [HarmonyPatch]
 static class JewelSpawn {
  static readonly MethodInfo? Spawn=OptionalTypes.InPlugin("org.bepinex.plugins.jewelcrafting","Jewelcrafting.Utils")?.GetMethod("DropPrefabItem",new[]{typeof(GameObject),typeof(Character)});
  static bool Prepare()=>Spawn!=null;
  static MethodBase TargetMethod()=>Spawn!;
  static void Postfix(GameObject __result,Character target){try{
   if(!__result||!target||target is Player||target.GetHealth()>0)return;
   var view=target.GetComponent<ZNetView>();var item=__result.GetComponent<ItemDrop>();
   if(!item||!view||!view.IsValid()||!view.IsOwner())return;
   Spawned[item.GetInstanceID()]=("jewelcrafting-creature",new Credit{Id=view.GetZDO().GetString("sagas.lastAttacker",""),Name=view.GetZDO().GetString("sagas.lastName","")});
  }catch{ /* Optional provenance failure leaves pickup unknown, never invents credit. */ }}
 }
 // Optional SLS replacement uses an iterator; scope each MoveNext, not coroutine creation.
 [HarmonyPatch]
 static class SlsDropScope {
  static bool Prepare()=>AccessTools.TypeByName("StarLevelSystem.modules.Loot.LootPerformanceChanges")!=null;
  static IEnumerable<MethodBase> TargetMethods(){var t=AccessTools.TypeByName("StarLevelSystem.modules.Loot.LootPerformanceChanges");if(t==null)yield break;foreach(var n in t.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public))if(n.Name.Contains("DropItemsAsync")){var m=AccessTools.Method(n,"MoveNext");if(m!=null)yield return m;}var immediate=AccessTools.Method(t,"DropItemsImmediate");if(immediate!=null)yield return immediate;}
  static void Prefix(object? __instance,MethodBase __originalMethod,object[] __args,out (bool active,Credit? previous) __state){bool active=false;__state=(false,currentCredit);if(__instance!=null)active=AccessTools.Field(__instance.GetType(),"dropThatCharacterDrop")?.GetValue(__instance) as bool? ?? false;else if(__args.Length>3)active=__args[3] as bool? ?? false;__state=(active,currentCredit);if(__instance!=null&&iteratorCredits.TryGetValue(__instance,out var credit))currentCredit=credit;if(active)CreatureDropDepth++;}
  static Exception? Finalizer((bool active,Credit? previous) __state,Exception? __exception){if(__state.active)CreatureDropDepth=Math.Max(0,CreatureDropDepth-1);currentCredit=__state.previous;return __exception;}
 }
}
