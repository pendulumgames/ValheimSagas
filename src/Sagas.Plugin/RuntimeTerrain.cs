using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
namespace ValheimSagas;
internal static class RuntimeTerrain {
 internal static void Clear()=>RuntimeTerrainShader.Clear();
 static readonly System.Reflection.FieldInfo heightField=AccessTools.Field(typeof(Minimap),"m_heightTexture"),forestField=AccessTools.Field(typeof(Minimap),"m_forestMaskTexture"),readyField=AccessTools.Field(typeof(Minimap),"m_hasGenerated");
 internal static string Capture(Minimap map,BitArray explored,int cx,int cz){
  if(!map.m_mapTexture||!(readyField.GetValue(map) is bool ready&&ready)||!RuntimeTerrainMask.MayContainKnownSquare(explored,map.m_textureSize,map.m_pixelSize,cx,cz,64))return "";
  var knownPixels=new bool[64*64];bool hasKnown=false;
  for(int z=0;z<64;z++)for(int x=0;x<64;x++)if(RuntimeTerrainMask.SquareKnown(explored,map.m_textureSize,map.m_pixelSize,cx*64+x,cz*64+z,1)){knownPixels[z*64+x]=true;hasKnown=true;}
  if(!hasKnown)return "";
  var rendered=RuntimeTerrainShader.Capture(map,cx,cz);
  if(rendered!=null){
   var output=new byte[64*64*4];
   for(int i=0;i<knownPixels.Length;i++)if(knownPixels[i]){output[i*4]=rendered[i*4];output[i*4+1]=rendered[i*4+1];output[i*4+2]=rendered[i*4+2];output[i*4+3]=255;}
   return Convert.ToBase64String(output);
  }
  // Explicitly degraded16x16 fallback, already logged by RuntimeTerrainShader.
  var heights=heightField.GetValue(map) as Texture2D;var forests=forestField.GetValue(map) as Texture2D;var bytes=new byte[16*16*4];bool any=false;
  for(int z=0;z<16;z++)for(int x=0;x<16;x++){
   // Require all source fog pixels touched by this 4m square, rather than a whole 64m cell.
   float wx=cx*64+x*4,wz=cz*64+z*4;
   int minx=Mathf.FloorToInt(wx/map.m_pixelSize+map.m_textureSize/2f),minz=Mathf.FloorToInt(wz/map.m_pixelSize+map.m_textureSize/2f);
   int maxx=Mathf.CeilToInt((wx+4)/map.m_pixelSize+map.m_textureSize/2f),maxz=Mathf.CeilToInt((wz+4)/map.m_pixelSize+map.m_textureSize/2f);
   if(minx<0||minz<0||maxx>=map.m_textureSize||maxz>=map.m_textureSize)continue;
   bool known=true;for(int py=minz;py<=maxz&&known;py++)for(int px=minx;px<=maxx;px++)if(!explored[py*map.m_textureSize+px]){known=false;break;}if(!known)continue;
   int sx=(minx+maxx)/2,sz=(minz+maxz)/2;var color=map.m_mapTexture.GetPixel(sx,sz);float h=heights?heights.GetPixel(sx,sz).r:40;
   if(h<30){float depth=Mathf.Clamp01((30-h)/100);color=Color.Lerp(new Color(.30f,.46f,.52f),new Color(.12f,.23f,.32f),depth);}
   else{float shade=1;if(heights){float dx=heights.GetPixel(Math.Min(sx+1,map.m_textureSize-1),sz).r-h;float dz=heights.GetPixel(sx,Math.Min(sz+1,map.m_textureSize-1)).r-h;shade=Mathf.Clamp(1+(dx-dz)*.025f,.65f,1.18f);}if(forests)shade*=1-forests.GetPixel(sx,sz).r*.23f;color*=shade;}
   Color32 rgb=color;int i=(z*16+x)*4;bytes[i]=rgb.r;bytes[i+1]=rgb.g;bytes[i+2]=rgb.b;bytes[i+3]=255;any=true;
  }
  return any?Convert.ToBase64String(bytes):"";
 }
}
