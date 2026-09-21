using ValheimSagas;
OptionalTypes.Enabled=!args.Contains("--absent");int checks=0;void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
var item=new ItemDrop.ItemData();
if(!OptionalTypes.Enabled){JewelcraftingAdapter.Read(item,out var absent,out var color);Check(absent.Count==0&&color=="","Absent mod no-op");Check(JewelcraftingAdapter.Equipped(new Player()).Count()==0,"Absent jewelry no-op");Console.WriteLine($"PASS {checks} absent-mod adapter checks.");return;}
JewelcraftingAdapter.Read(item,out var empty,out _);Check(empty.Count==0&&FakeAPI.Calls==0,"Non-socket gear/bags never enumerate contents");
item.m_shared.m_name="armored-ring";Check(JewelcraftingAdapter.HasArmor(item),"Installed jewelry armor classification read without guessing item names");item.Socketed=true;FakeAPI.Gems=new(){new FakeAPI.GemInfo(),null};FakeAPI.Gems[0]!.gemPrefab="ruby";FakeAPI.Gems[0]!.gemEffectsPowerRange=new(){{"Power",new[]{1f,3f}},{"Broken",new[]{float.NaN,1f}}};
ObjectDB.instance.Items["ruby"]=new UnityEngine.GameObject(new ItemDrop{m_itemData=new(){m_shared=new(){m_name="<color=red>Ruby</color>"}}});
JewelcraftingAdapter.Read(item,out var sockets,out var shade);Check(sockets.Count==2&&sockets[1].Prefab=="","Empty slot ordering preserved");Check(sockets[0].Name=="Ruby"&&shade=="#AABBCC","Localized plain gem name and socket palette");Check(sockets[0].Effects.Single()=="Power: 1–3 (configured range)","Actual range not midpoint; invalid power excluded");
var gear=new GearItem{Sockets=sockets};int captured=0;JewelcraftingAdapter.Icons(gear,i=>{captured++;return "synthetic";});Check(captured==1&&sockets[0].IconId=="synthetic","Only known filled gems enqueue artwork");
var player=new Player();player.Items.Add(item);FakeAPI.Jewelry.equippedFingerItem=item;FakeAPI.Jewelry.equippedNeckItem=new();Check(JewelcraftingAdapter.Equipped(player).Count()==2,"Jewelry collected without duplicating inventory gear");Check(player.Items.Count==1,"Inventory remains untouched");JewelcraftingAdapter.JewelrySlot(gear,item,player);Check(gear.Slot=="Ring"&&gear.Equipped,"Extra ring slot marked equipped");
FakeAPI.Gems=Enumerable.Range(0,100).Select(_=>(FakeAPI.GemInfo?)null).ToList();JewelcraftingAdapter.Read(item,out var bounded,out _);Check(bounded.Count==11,"Socket metadata bounded");FakeAPI.Throw=true;JewelcraftingAdapter.Read(item,out _,out _);JewelcraftingAdapter.Read(item,out _,out _);Check(UnityEngine.Debug.Warnings==1,"Optional API failure contained and warning rate limited");Console.WriteLine($"PASS {checks} synthetic adapter contract checks; no game code executed.");
namespace ValheimSagas {
 internal static class OptionalTypes {public static bool Enabled;public static Type? InPlugin(string guid,string name)=>!Enabled?null:name.EndsWith(".API")?typeof(FakeAPI):name.EndsWith(".JewelrySetup")?typeof(FakeJewelrySetup):typeof(FakeVisual);}
 internal static class SagasPlugin {public static string Localize(string s)=>s;}
}
public static class FakeAPI {
 public class GemInfo {public string gemPrefab="";public Dictionary<string,float[]> gemEffectsPowerRange=new();}
 public static bool Throw;public static int Calls;public static List<GemInfo?> Gems=new();public static FakeVisual Jewelry=new();
 public static UnityEngine.Color? GetSocketableItemColor(ItemDrop.ItemData i){if(Throw)throw new Exception();return i.Socketed?new UnityEngine.Color():null;}
 public static List<GemInfo?> GetGems(ItemDrop.ItemData i){Calls++;return Gems;}
 public static FakeVisual GetEquippedJewelry(Player p)=>Jewelry;
}
public class FakeJewelrySetup {public static HashSet<string> upgradeableJewelry=new(){"armored-ring"};}
public class FakeVisual {public ItemDrop.ItemData? equippedFingerItem,equippedNeckItem;}
public class ItemDrop {public ItemData m_itemData=new();public class ItemData {public bool Socketed;public Shared m_shared=new();}public class Shared {public string m_name="";}}
public class Player {public List<ItemDrop.ItemData> Items=new();public Player GetInventory()=>this;public List<ItemDrop.ItemData> GetEquippedItems()=>new(Items);}
public class ObjectDB {public static ObjectDB instance=new();public Dictionary<string,UnityEngine.GameObject> Items=new();public UnityEngine.GameObject? GetItemPrefab(string s)=>Items.GetValueOrDefault(s);public static implicit operator bool(ObjectDB? o)=>o!=null;}
namespace UnityEngine {
 public struct Color {}
 public static class ColorUtility {public static string ToHtmlStringRGB(Color c)=>"AABBCC";}
 public static class Time {public static float unscaledTime=1;}
 public static class Debug {public static int Warnings;public static void LogWarning(string s)=>Warnings++;}
 public class GameObject {readonly object component;public GameObject(object c){component=c;}public T? GetComponent<T>() where T:class=>component as T;public static implicit operator bool(GameObject? g)=>g!=null;}
}
