using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
namespace ValheimSagas;
public sealed class MapPin {
 public string Name {get;set;}=""; public string Type {get;set;}=""; public string IconId {get;set;}="";
 public float X {get;set;} public float Z {get;set;} public bool Personal {get;set;} public bool Checked {get;set;}
}
public sealed class MapPins {
 public string World {get;set;}=""; public string PlayerId {get;set;}=""; public string Revision {get;set;}="";
 public int Index {get;set;} public int Count {get;set;}=1; public List<MapPin> Pins {get;set;}=new List<MapPin>();
}
public sealed class WorldClock {
 public string World {get;set;}=""; public int Day {get;set;} public float Fraction {get;set;} public DateTime Utc {get;set;}=DateTime.UtcNow;
}
public sealed partial class SagaStore {
 public void SavePins(MapPins pins){lock(gate){var row=Wrap(Key(pins.World,pins.PlayerId),pins.World,pins.PlayerId,pins,DateTime.UtcNow);row["revision"]=pins.Revision;db.GetCollection("mapPins").Upsert(row);}}
 public List<MapPins> Pins(string world){lock(gate)return db.GetCollection("mapPins").Find(Query.EQ("world",world)).Select(Read<MapPins>).ToList();}
 public string PinRevision(string world,string player){lock(gate){var row=db.GetCollection("mapPins").FindById(Key(world,player));return row==null?"":row["revision"].AsString;}}
}
public sealed partial class SagaService {
 sealed class PinAssembly {internal DateTime Started=DateTime.UtcNow;internal string Revision="";internal MapPins?[] Parts=Array.Empty<MapPins?>();internal Action<bool>?[] Callbacks=Array.Empty<Action<bool>?>();}
 readonly Dictionary<string,PinAssembly> pinAssemblies=new Dictionary<string,PinAssembly>();
 WorldClock? worldClock;
 public void UpdateClock(WorldClock clock){if(clock.World!=options.World||clock.Day<0||!Finite(clock.Fraction)||clock.Fraction<0||clock.Fraction>1)return;System.Threading.Volatile.Write(ref worldClock,new WorldClock{World=clock.World,Day=clock.Day,Fraction=clock.Fraction});}
 public WorldClock? Clock(string world){var clock=System.Threading.Volatile.Read(ref worldClock);return clock?.World==world?clock:null;}
 public bool UpdatePins(MapPins batch,Action<bool>? committed=null){
  if(batch==null||!TextValid(batch.World,100,true)||!TextValid(batch.PlayerId,100,true)||!TextValid(batch.Revision,64,true)||batch.Count<1||batch.Count>64||batch.Index<0||batch.Index>=batch.Count||batch.Pins==null||batch.Pins.Count>32||batch.Pins.Any(p=>p==null||!TextValid(p.Name,100)||!TextValid(p.Type,60)||!MediaId(p.IconId)||!Coordinate(p.X)||!Coordinate(p.Z)))return false;
  var copy=Copy(batch);return Enqueue(()=>{
   // Lost acknowledgements must not require retransmitting already acknowledged parts.
   if(store.PinRevision(copy.World,copy.PlayerId)==copy.Revision){committed?.Invoke(true);return;}
   foreach(var key in pinAssemblies.Where(p=>(DateTime.UtcNow-p.Value.Started).TotalMinutes>10).Select(p=>p.Key).ToArray())pinAssemblies.Remove(key);
   var owner=copy.World+":"+copy.PlayerId;
   if(!pinAssemblies.TryGetValue(owner,out var assembly)||assembly.Revision!=copy.Revision){if(assembly==null&&pinAssemblies.Count>=64)return;assembly=new PinAssembly{Revision=copy.Revision,Parts=new MapPins?[copy.Count],Callbacks=new Action<bool>?[copy.Count]};pinAssemblies[owner]=assembly;}
   if(assembly.Parts.Length!=copy.Count)return;
   assembly.Parts[copy.Index]=copy;assembly.Callbacks[copy.Index]=committed;
   if(assembly.Parts.Any(p=>p==null))return;
   store.SavePins(new MapPins{World=copy.World,PlayerId=copy.PlayerId,Revision=copy.Revision,Pins=assembly.Parts.SelectMany(p=>p!.Pins).ToList()});
   pinAssemblies.Remove(owner);foreach(var callback in assembly.Callbacks)callback?.Invoke(true);
  });
 }
 // Check the owner's own shared fog as well as the selected combined map. A second
 // Viking's exploration must never reveal a private/unexplored pin from this owner.
 public object MapIcons(string world,HashSet<string>? selected=null){return new{pins=VisiblePins(world,selected)};}
 internal object[] VisiblePins(string world,HashSet<string>? selected=null){
  var owners=store.Players(world).Where(p=>p.ShareMap&&(selected==null||selected.Contains(p.PlayerId))).ToDictionary(p=>p.PlayerId);
  var result=new List<object>();var gamePins=new HashSet<string>();
  foreach(var snapshot in store.Pins(world)){
   if(!owners.TryGetValue(snapshot.PlayerId,out var owner))continue;
   var cells=store.Cells(world,new HashSet<string>{owner.PlayerId}).ToDictionary(c=>c.X+":"+c.Z);
   foreach(var pin in snapshot.Pins){
    if(pin.Personal&&!owner.SharePins)continue;
    if(!cells.TryGetValue((int)Math.Floor(pin.X/64)+":"+(int)Math.Floor(pin.Z/64),out var cell)||!TerrainTile.Known(cell,pin.X,pin.Z))continue;
    var key=pin.Type+":"+pin.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+pin.Z.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
    if(!pin.Personal&&!gamePins.Add(key))continue;
    result.Add(new{pin.Name,pin.Type,pin.IconId,pin.X,pin.Z,pin.Personal,pin.Checked,playerId=owner.PlayerId,owner=owner.Name});
   }
  }return result.ToArray();
 }
 internal bool VisiblePinMedia(string world,string player,string id){
  if(!store.IsMapIcon(world,player,id))return false;
  var owner=store.Players(world).FirstOrDefault(p=>p.PlayerId==player&&p.ShareMap);if(owner==null)return false;
  var pins=store.Pins(world).FirstOrDefault(p=>p.PlayerId==player);if(pins==null)return false;
  var cells=store.Cells(world,new HashSet<string>{player}).ToDictionary(c=>c.X+":"+c.Z);
  return pins.Pins.Any(p=>p.IconId==id&&(!p.Personal||owner.SharePins)&&cells.TryGetValue((int)Math.Floor(p.X/64)+":"+(int)Math.Floor(p.Z/64),out var cell)&&TerrainTile.Known(cell,p.X,p.Z));
 }
}
