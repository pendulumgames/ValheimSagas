using ValheimSagas;
using Newtonsoft.Json.Linq;
using LiteDB;
static class MapDeliveryChecks {
 static JObject Json(object value)=>JObject.FromObject(value);
 public static void Run(string root,Action<bool,string> check){
  string path=Path.Combine(root,"map-delivery");string scope="",cursor="";var received=new HashSet<int>();
  string Pixels(Func<int,int,bool> known,byte color){var bytes=new byte[16384];for(int z=0;z<64;z++)for(int x=0;x<64;x++)if(known(x,z)){int i=(z*64+x)*4;bytes[i]=color;bytes[i+3]=255;}return Convert.ToBase64String(bytes);}
  using(var service=new SagaService(new SagaOptions{DataDirectory=path,World="world"})){
   service.Store.Player(new(){World="world",PlayerId="a",ShareMap=true});service.Store.Player(new(){World="world",PlayerId="b",ShareMap=true});service.Store.Player(new(){World="world",PlayerId="hidden",ShareMap=false});
   service.Store.Explore(new(){World="world",PlayerId="a",Cells=Enumerable.Range(0,300).Select(x=>new MapCell{X=x,Z=0,Biome="Meadows",TerrainPixels=Pixels((px,pz)=>px<32,40)}).ToList()});
   service.Store.Explore(new(){World="world",PlayerId="b",Cells=new(){new(){X=0,Z=0,TerrainPixels=Pixels((x,z)=>x>=32,80)}}});
   service.Store.Explore(new(){World="world",PlayerId="hidden",Cells=new(){new(){X=-1,Z=-1,TerrainPixels=Pixels((x,z)=>true,200)}}});
   bool more=true;int pages=0;while(more){var page=Json(service.MapStream("world",null,scope,cursor));check((bool)page["reset"]! == (pages==0),"Map scope reset only initial page");scope=(string)page["scope"]!;cursor=(string)page["cursor"]!;more=(bool)page["more"]!;var cells=(JArray)page["cells"]!;check(cells.Count<=256,"Stream page bounded");check(cells.All(c=>(int)c["X"]!>=0),"Private map excluded from stream");foreach(var row in cells)received.Add((int)row["X"]!);pages++;check(pages<10,"Map stream makes progress");}
   check(received.Count==300,"Paged overview delivers unique current coordinates");
   var quiet=Json(service.MapStream("world",null,scope,cursor));check(!quiet["cells"]!.Any()&&!(bool)quiet["more"]!,"Unchanged map poll sends no terrain");
   var detail=Json(service.MapDetail("world",null,"0,0;-1,-1",16));check(detail["cells"]!.Count()==1,"Detail omits private tiles");var cell=detail["cells"]![0]!;check(Convert.FromBase64String((string)cell["TerrainPixels"]!).Length==1024,"Requested 16px detail bounded");check(Convert.FromBase64String((string)cell["ExplorationMask"]!).All(b=>b==255),"Exact selected exploration masks union");
   var personal=Json(service.MapDetail("world",new(){"a"},"0,0",64));var m=Convert.FromBase64String((string)personal["cells"]![0]!["ExplorationMask"]!);check(m[0]==255&&m[7]==0,"Personal mask excludes other owner's discovery");
   service.Store.Player(new(){World="world",PlayerId="b",ShareMap=false});var revoked=Json(service.MapStream("world",null,scope,cursor));check((bool)revoked["reset"]!&&(string)revoked["scope"]! !=scope,"Sharing revocation resets all client terrain");
   service.Store.Explore(new(){World="world",PlayerId="a",Cells=new(){new(){X=0,Z=0,TerrainPixels=Pixels((x,z)=>true,120)}}});var changed=Json(service.MapStream("world",new(){"a"},(string)revoked["scope"]!,cursor));check(changed["cells"]!.Count()==1,"Changed tile sent after previous cursor");
   service.Store.Explore(new(){World="world",PlayerId="a",Cells=new(){new(){X=-2,Z=0,TerrainPixels=Pixels((x,z)=>x==0&&z==0,255)}}});
   var partial=Json(service.MapStream("world",new(){"a"},(string)changed["scope"]!,(string)changed["cursor"]!));var partialCell=partial["cells"]!.First(c=>(int)c["X"]! ==-2);var partialPixels=Convert.FromBase64String((string)partialCell["TerrainPixels"]!);check(partialPixels.All(x=>x==0),"Partial overview block stays fogged instead of leaking coarse terrain");check(Convert.FromBase64String((string)partialCell["ExplorationMask"]!)[0]==1,"Fine exploration bit survives conservative overview");
   service.Store.Sls("world",new(){Installed=true,ZoneScalingEnabled=true,OverlayEnabled=true,AboveFog=false,Zones=new(){new(){MinX=0,MaxX=64,MinZ=0,MaxZ=64},new(){MinX=-64,MaxX=0,MinZ=-64,MaxZ=0}}});
   var meta=Json(service.MapMeta("world"));check(meta["sls"]!["zones"]!.Count()==1,"Below-fog zone metadata excludes privately explored zone");service.Store.Sls("world",new(){Installed=true,ZoneScalingEnabled=true,OverlayEnabled=true,AboveFog=true,Zones=new(){new(){MinX=-64,MaxX=0,MinZ=-64,MaxZ=0}}});check(Json(service.MapMeta("world"))["sls"]!["zones"]!.Count()==1,"Above-fog SLS policy includes unexplored zone");
   foreach(var invalid in new[]{"foo", "314,0",string.Join(";",Enumerable.Range(0,65).Select(x=>x+",0"))}){bool rejected=false;try{service.MapDetail("world",null,invalid);}catch(ArgumentException){rejected=true;}check(rejected,"Invalid or oversized detail request rejected");}
   check(service.Store.KnownPoint("world",new(){"a"},2,2)&&!service.Store.KnownPoint("world",new(){"a"},-2,-2),"Point lookup enforces exact owner tiles");
  }
  using(var service=new SagaService(new SagaOptions{DataDirectory=path,World="world"})){var restart=Json(service.MapStream("world",null,scope,cursor));check((bool)restart["reset"]!,"Restart invalidates cached scope");check(restart["cells"]!.Any(),"Persistent compact index survives restart");}
  // Emulate a pre-index database without involving any real world or save files.
  string legacy=Path.Combine(root,"map-delivery-legacy");using(var store=new SagaStore(legacy)){store.Player(new(){World="world",PlayerId="a",ShareMap=true});store.Explore(new(){World="world",PlayerId="a",Cells=Enumerable.Range(0,300).Select(x=>new MapCell{X=x,Z=0}).ToList()});}
  using(var db=new LiteDatabase(Path.Combine(legacy,"sagas.db"))){db.DropCollection("mapOverview");db.GetCollection("meta").Delete("mapOverviewMigration");}
  using(var store=new SagaStore(legacy)){check(store.MigrateMapPage(),"Legacy migration reports pending rows");check(store.MapPage("world",new(){"a"},0).Cells.Count==128,"Legacy migration converts at most128 tiles");check(store.MigrateMapPage()&&!store.MigrateMapPage(),"Legacy migration completes in bounded batches");check(store.MapDetail("world",new(){"a"},new[]{(299,0)},64).Count==1,"Legacy detail remains available during migration");}
 }
}
