using ValheimSagas;
static class RenderedTerrainChecks {
 public static void Run(Action<bool,string> check){
  var high=new byte[TerrainTile.RenderedByteCount];high[0]=190;high[3]=255;
  var h=new MapCell{X=-1,Z=-1,TerrainPixels=Convert.ToBase64String(high)};
  check(TerrainTile.Valid(h.TerrainPixels)&&TerrainTile.Decode(h.TerrainPixels)!.Length==16384,"Rendered terrain accepts exact 64x64 RGBA size");
  check(TerrainTile.Known(h,-64,-64)&&TerrainTile.Known(h,-63.001f,-63.001f)&&!TerrainTile.Known(h,-63,-64),"Rendered terrain retains exact one-meter fog boundaries");
  high[7]=1;check(!TerrainTile.Valid(Convert.ToBase64String(high)),"Rendered terrain rejects semitransparent unknown pixels");high[7]=0;high[4]=100;check(!TerrainTile.Valid(Convert.ToBase64String(high)),"Rendered terrain strips hidden RGB");high[4]=0;
  var low=new byte[TerrainTile.ByteCount];low[0]=50;low[3]=255;var l=new MapCell{X=-1,Z=-1,TerrainPixels=Convert.ToBase64String(low)};
  foreach(var pair in new[]{new[]{h,l},new[]{l,h}}){var union=TerrainTile.Union(pair);var bytes=TerrainTile.Decode(union.TerrainPixels)!;check(bytes.Length==16384&&bytes[0]==190&&bytes[4]==50,"Mixed-resolution union retains finest terrain independent of contributor order");check(TerrainTile.Known(union,-61,-61)&&!TerrainTile.Known(union,-60,-64),"Coarse discoveries cover only their original known footprint in fine union");}
  var second=(byte[])high.Clone();second[3]=0;second[0]=0;second[7]=255;second[4]=80;var other=new MapCell{X=-1,Z=-1,TerrainPixels=Convert.ToBase64String(second)};
  var fine=TerrainTile.Union(new[]{h,other});check(TerrainTile.Known(fine,-63,-64)&&!TerrainTile.Known(fine,-62,-64),"Two rendered personal maps union without exposing adjacent fog");
  check(!TerrainTile.Known(new MapCell{X=0,Z=0},64,0),"Legacy known tiles still enforce coordinate bounds");
 }
}
