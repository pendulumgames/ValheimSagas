using System;
using System.Collections;
namespace ValheimSagas;

// Pure conservative early rejection. A true result grants no permission to read
// terrain: Capture still checks every source fog pixel of each exported square.
internal static class RuntimeTerrainMask {
 internal static bool MayContainKnownSquare(BitArray explored,int size,float pixelSize,int cx,int cz,int subdivisions=16) {
  if(subdivisions!=16&&subdivisions!=64)return false;
  if(size<=0||pixelSize<=0||float.IsNaN(pixelSize)||float.IsInfinity(pixelSize)||explored.Length<(long)size*size)return false;
  int minx=(int)Math.Floor(cx*64f/pixelSize+size/2f),minz=(int)Math.Floor(cz*64f/pixelSize+size/2f);
  int maxx=(int)Math.Ceiling((cx*64f+64)/pixelSize+size/2f),maxz=(int)Math.Ceiling((cz*64f+64)/pixelSize+size/2f);
  minx=Math.Max(minx,0);minz=Math.Max(minz,0);maxx=Math.Min(maxx,size-1);maxz=Math.Min(maxz,size-1);
  if(minx>maxx||minz>maxz)return false;
  if((long)(maxx-minx+1)*(maxz-minz+1)<=subdivisions*subdivisions) {
   // Vanilla/coarser source maps: one compact rectangle, no repeated samples or
   // per-subpixel float math. Never cache rejection: exploration can change.
   for(int z=minz;z<=maxz;z++)for(int x=minx;x<=maxx;x++)if(explored[z*size+x])return true;
  }else{
   // Fine-resolution modded maps: bounded mandatory corner tests. A fully
   // known square necessarily has its minimum corner known, so no false reject.
   float step=64f/subdivisions;
   for(int z=0;z<subdivisions;z++) {
    int pz=(int)Math.Floor((cz*64f+z*step)/pixelSize+size/2f);if(pz<0||pz>=size)continue;
    for(int x=0;x<subdivisions;x++) {
     int px=(int)Math.Floor((cx*64f+x*step)/pixelSize+size/2f);
     if(px>=0&&px<size&&explored[pz*size+px])return true;
    }
   }
  }
  return false;
 }
 internal static bool SquareKnown(BitArray explored,int size,float pixelSize,float wx,float wz,float step){
  if(size<=0||pixelSize<=0||float.IsNaN(pixelSize)||float.IsInfinity(pixelSize)||explored.Length<(long)size*size)return false;
  int minx=(int)Math.Floor(wx/pixelSize+size/2f),minz=(int)Math.Floor(wz/pixelSize+size/2f);
  int maxx=(int)Math.Ceiling((wx+step)/pixelSize+size/2f),maxz=(int)Math.Ceiling((wz+step)/pixelSize+size/2f);
  if(minx<0||minz<0||maxx>=size||maxz>=size)return false;
  for(int z=minz;z<=maxz;z++)for(int x=minx;x<=maxx;x++)if(!explored[z*size+x])return false;
  return true;
 }
}
