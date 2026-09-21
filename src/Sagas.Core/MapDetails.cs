using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
namespace ValheimSagas;
public class MapPin {
 public string Name {get;set;}=""; public string Type {get;set;}=""; public string IconId {get;set;}="";
 public float X {get;set;} public float Z {get;set;} public bool Personal {get;set;} public bool Checked {get;set;}
}
public sealed class MapPinSource {public string PlayerId {get;set;}="";public string Name {get;set;}="";public string Color {get;set;}="";public string IconId {get;set;}="";}
public sealed class VisibleMapPin:MapPin {public string PlayerId {get;set;}="";public string Owner {get;set;}="";public List<MapPinSource> Sources {get;set;}=new List<MapPinSource>();}
public sealed class MapPins {
 public string World {get;set;}=""; public string PlayerId {get;set;}=""; public string Revision {get;set;}="";
 public int Index {get;set;} public int Count {get;set;}=1; public List<MapPin> Pins {get;set;}=new List<MapPin>();
}
public sealed class WorldClock {
 public string World {get;set;}=""; public int Day {get;set;} public float Fraction {get;set;} public DateTime Utc {get;set;}=DateTime.UtcNow;
 public double? SecondsToTransition {get;set;} public string NextPhase {get;set;}="";public bool Running {get;set;}public bool Skipping {get;set;}
 public static WorldClock Sample(string world,int day,float fraction,double seconds,double dayLength,double timeScale,bool advancing,bool skipping){
  var c=new WorldClock{World=world,Day=day,Fraction=fraction,Skipping=skipping};
  if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0||dayLength<=0||double.IsInfinity(dayLength)||double.IsNaN(dayLength))return c;
  var phase=seconds%dayLength;var dawn=dayLength*.15;var dusk=dayLength*.85;
  c.NextPhase=phase<dawn||phase>=dusk?"day":"night";
  var remaining=phase<dawn?dawn-phase:phase<dusk?dusk-phase:dayLength-phase+dawn;
  c.Running=advancing&&!skipping&&timeScale>0&&!double.IsInfinity(timeScale)&&!double.IsNaN(timeScale);
  if(c.Running)c.SecondsToTransition=remaining/timeScale;return c;
 }
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
 public void UpdateClock(WorldClock clock){if(clock.World!=options.World||clock.Day<0||!Finite(clock.Fraction)||clock.Fraction<0||clock.Fraction>1||clock.SecondsToTransition.HasValue&&(double.IsNaN(clock.SecondsToTransition.Value)||double.IsInfinity(clock.SecondsToTransition.Value)||clock.SecondsToTransition<0)||clock.NextPhase!=""&&clock.NextPhase!="day"&&clock.NextPhase!="night")return;System.Threading.Volatile.Write(ref worldClock,new WorldClock{World=clock.World,Day=clock.Day,Fraction=clock.Fraction,SecondsToTransition=clock.SecondsToTransition,NextPhase=clock.NextPhase,Running=clock.Running,Skipping=clock.Skipping});}
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
 // Automatic locations require the owner's personal fog. Explicitly shared personal
 // annotations may mark unexplored ground, matching their in-game behavior.
 public object MapIcons(string world,HashSet<string>? selected=null){return new{pins=VisiblePins(world,selected)};}
 internal VisibleMapPin[] VisiblePins(string world,HashSet<string>? selected=null){
  var owners=store.Players(world).Where(p=>p.ShareMap&&(selected==null||selected.Contains(p.PlayerId))).ToDictionary(p=>p.PlayerId);
  var result=new List<VisibleMapPin>();var gamePins=new Dictionary<string,VisibleMapPin>();
  foreach(var snapshot in store.Pins(world)){
   if(!owners.TryGetValue(snapshot.PlayerId,out var owner))continue;
   var cells=store.Cells(world,new HashSet<string>{owner.PlayerId}).ToDictionary(c=>c.X+":"+c.Z);
   foreach(var pin in snapshot.Pins){
    if(pin.Personal&&!owner.SharePins)continue;
    if(!pin.Personal&&(!cells.TryGetValue((int)Math.Floor(pin.X/64)+":"+(int)Math.Floor(pin.Z/64),out var cell)||!TerrainTile.Known(cell,pin.X,pin.Z)))continue;
    var key=pin.Type+":"+pin.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+pin.Z.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
    var source=new MapPinSource{PlayerId=owner.PlayerId,Name=owner.Name,Color=owner.MapColor,IconId=pin.IconId};
    if(!pin.Personal&&gamePins.TryGetValue(key,out var shared)){if(!shared.Sources.Any(p=>p.PlayerId==owner.PlayerId))shared.Sources.Add(source);continue;}
    var view=new VisibleMapPin{Name=pin.Name,Type=pin.Type,IconId=pin.IconId,X=pin.X,Z=pin.Z,Personal=pin.Personal,Checked=pin.Checked,PlayerId=owner.PlayerId,Owner=owner.Name,Sources=new List<MapPinSource>{source}};
    result.Add(view);if(!pin.Personal)gamePins[key]=view;
   }
  }return result.ToArray();
 }
 internal bool VisiblePinMedia(string world,string player,string id){
  if(!store.IsMapIcon(world,player,id))return false;
  var owner=store.Players(world).FirstOrDefault(p=>p.PlayerId==player&&p.ShareMap);if(owner==null)return false;
  var pins=store.Pins(world).FirstOrDefault(p=>p.PlayerId==player);if(pins==null)return false;
  var cells=store.Cells(world,new HashSet<string>{player}).ToDictionary(c=>c.X+":"+c.Z);
  return pins.Pins.Any(p=>p.IconId==id&&(!p.Personal||owner.SharePins)&&(p.Personal||cells.TryGetValue((int)Math.Floor(p.X/64)+":"+(int)Math.Floor(p.Z/64),out var cell)&&TerrainTile.Known(cell,p.X,p.Z)));
 }
}
