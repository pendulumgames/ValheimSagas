using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
namespace ValheimSagas;
public sealed class SlsZone {
 public float MinX {get;set;} public float MaxX {get;set;} public float MinZ {get;set;} public float MaxZ {get;set;}
 public int Level {get;set;}=1; public string Color {get;set;}="#808080";
}
public sealed class SlsWorldState {
 public bool Installed {get;set;} public bool NemesisEnabled {get;set;} public bool ZoneScalingEnabled {get;set;}
 public bool OverlayEnabled {get;set;} public bool AboveFog {get;set;} public float Opacity {get;set;}=.6f;
 public float OutlineInset {get;set;}=12; public DateTime Utc {get;set;}=DateTime.UtcNow;
 public List<SlsZone> Zones {get;set;}=new List<SlsZone>();
}
public sealed partial class SagaStore {
 public SlsWorldState Sls(string world){lock(gate){var row=db.GetCollection("slsWorlds").FindById(Key(world));return row==null?new SlsWorldState():Read<SlsWorldState>(row);}}
 public void Sls(string world,SlsWorldState value){lock(gate)db.GetCollection("slsWorlds").Upsert(Wrap(Key(world),world,"",value,value.Utc));}
}
public sealed partial class SagaService {
 public bool UpdateSls(string world,SlsWorldState value){
  if(!TextValid(world,100,true)||value==null||value.Zones==null||value.Zones.Count>40000||!Finite(value.Opacity)||value.Opacity<0||value.Opacity>1||!Finite(value.OutlineInset)||value.OutlineInset<0||value.OutlineInset>100||value.Zones.Any(z=>z==null||!Coordinate(z.MinX)||!Coordinate(z.MaxX)||!Coordinate(z.MinZ)||!Coordinate(z.MaxZ)||z.MinX>=z.MaxX||z.MinZ>=z.MaxZ||z.Level<0||!RarityColors.Valid(z.Color)||z.Color==""))return false;
  var copy=Copy(value);copy.Utc=DateTime.UtcNow;if(!copy.Installed){copy.NemesisEnabled=false;copy.ZoneScalingEnabled=false;copy.OverlayEnabled=false;copy.Zones.Clear();}if(!copy.ZoneScalingEnabled||!copy.OverlayEnabled)copy.Zones.Clear();return Enqueue(()=>store.Sls(world,copy));
 }
 static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
 static object SlsSummary(SlsWorldState value)=>new{value.Installed,value.NemesisEnabled,value.ZoneScalingEnabled,value.OverlayEnabled,value.AboveFog,value.Opacity,value.OutlineInset,value.Utc};
 // Below-fog metadata is restricted to zones with at least one permitted explored sample.
 // The remaining outline is clipped against those same samples by the renderer.
 SlsWorldState ActiveSls(string world)=>options.SlsInstalled==false?new SlsWorldState():store.Sls(world);
 object SlsMap(string world,List<MapCell> cells){
  var value=ActiveSls(world);var zones=value.Installed&&value.ZoneScalingEnabled&&value.OverlayEnabled?value.Zones:new List<SlsZone>();
  if(!value.AboveFog&&zones.Count>0){
   var tiles=cells.ToDictionary(c=>(c.X,c.Z),c=>(Cell:c,Bytes:TerrainTile.Decode(c.TerrainPixels)));
   bool Visible(SlsZone zone){
    for(int z=(int)Math.Floor(zone.MinZ/64);z<Math.Ceiling(zone.MaxZ/64);z++)for(int x=(int)Math.Floor(zone.MinX/64);x<Math.Ceiling(zone.MaxX/64);x++){
     if(!tiles.TryGetValue((x,z),out var tile))continue;if(string.IsNullOrEmpty(tile.Cell.TerrainPixels))return true;if(tile.Bytes==null)continue;
     var side=tile.Bytes.Length==TerrainTile.RenderedByteCount?64:16;var step=64f/side;
     for(int py=Math.Max(0,(int)Math.Floor((zone.MinZ-z*64)/step));py<Math.Min(side,(int)Math.Ceiling((zone.MaxZ-z*64)/step));py++)for(int px=Math.Max(0,(int)Math.Floor((zone.MinX-x*64)/step));px<Math.Min(side,(int)Math.Ceiling((zone.MaxX-x*64)/step));px++)if(tile.Bytes[(py*side+px)*4+3]==255)return true;
    }return false;
   }
   zones=zones.Where(Visible).ToList();
  }
  return new{value.Installed,value.NemesisEnabled,value.ZoneScalingEnabled,value.OverlayEnabled,value.AboveFog,value.Opacity,value.OutlineInset,value.Utc,zones};
 }
}
