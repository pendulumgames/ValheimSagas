using System;
using System.Collections.Generic;
using System.Linq;
namespace ValheimSagas;
// Legacy 16x16 or shader-rendered 64x64 RGBA per 64m tile; row zero is south.
public static class TerrainTile {
 public const int Width=16,ByteCount=1024,RenderedWidth=64,RenderedByteCount=16384;
 public static byte[]? Decode(string? text){if(string.IsNullOrEmpty(text))return null;try{if(text.Length!=1368&&text.Length!=21848)return null;var b=Convert.FromBase64String(text);return b.Length==ByteCount||b.Length==RenderedByteCount?b:null;}catch(FormatException){return null;}}
 public static bool Valid(string? text){if(text=="")return true;var b=Decode(text);if(b==null)return false;for(int i=0;i<b.Length;i+=4)if(b[i+3]!=255&&(b[i+3]!=0||b[i]!=0||b[i+1]!=0||b[i+2]!=0))return false;return true;}
 public static bool Known(MapCell c,float x,float z){var b=Decode(c.TerrainPixels);int side=b?.Length==RenderedByteCount?RenderedWidth:Width;int ix=(int)Math.Floor((x-c.X*64)*side/64),iz=(int)Math.Floor((z-c.Z*64)*side/64);if(ix<0||ix>=side||iz<0||iz>=side)return false;return string.IsNullOrEmpty(c.TerrainPixels)||b!=null&&b[(iz*side+ix)*4+3]==255;}
 public static MapCell Union(IEnumerable<MapCell> group){var list=group.ToList();var first=list[0];if(list.Count==1)return first;var decoded=list.Select(c=>new{Cell=c,Bytes=Decode(c.TerrainPixels)}).ToList();var legacy=decoded.FirstOrDefault(c=>c.Bytes==null);if(legacy!=null)return legacy.Cell;int side=decoded.Any(c=>c.Bytes!.Length==RenderedByteCount)?RenderedWidth:Width;var pixels=new byte[side*side*4];
  // Coarse samples first; finer terrain wins where both players know a pixel.
  foreach(var c in decoded.OrderBy(c=>c.Bytes!.Length)){var b=c.Bytes!;int source=b.Length==RenderedByteCount?RenderedWidth:Width;for(int z=0;z<side;z++)for(int x=0;x<side;x++){int from=((z*source/side)*source+x*source/side)*4;if(b[from+3]==255)Array.Copy(b,from,pixels,(z*side+x)*4,4);}}
  return new MapCell{X=first.X,Z=first.Z,Biome=first.Biome,Height=first.Height,TerrainPixels=Convert.ToBase64String(pixels)};
 }
}
