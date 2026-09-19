using System;
using System.Collections;
using System.Collections.Generic;
namespace ValheimSagas;

// Pure scan math: no Unity objects, terrain sampling, or exported cartography data.
internal static class ExplorationScan {
 internal const int Width=320;
 internal static IEnumerable<(int X,int Z)> Nearby(float x,float z) {
  int cx=(int)Math.Floor(x/64),cz=(int)Math.Floor(z/64);
  for(int radius=0;radius<=4;radius++)
   for(int dz=-radius;dz<=radius;dz++)for(int dx=-radius;dx<=radius;dx++) {
    if(Math.Max(Math.Abs(dx),Math.Abs(dz))!=radius)continue;
    int nx=cx+dx,nz=cz+dz;
    if(nx>=-Width/2&&nx<Width/2&&nz>=-Width/2&&nz<Width/2)yield return(nx,nz);
   }
 }
 internal static bool FullyKnown(BitArray bits,int size,float pixelSize,int cx,int cz) {
  if(size<=0||pixelSize<=0||bits.Length<(long)size*size)return false;
  int minx=(int)Math.Floor(cx*64/pixelSize+size/2f),minz=(int)Math.Floor(cz*64/pixelSize+size/2f);
  int maxx=(int)Math.Ceiling((cx+1)*64/pixelSize+size/2f),maxz=(int)Math.Ceiling((cz+1)*64/pixelSize+size/2f);
  if(minx<0||minz<0||maxx>=size||maxz>=size)return false;
  for(int z=minz;z<=maxz;z++)for(int x=minx;x<=maxx;x++)if(!bits[z*size+x])return false;
  return true;
 }
}
