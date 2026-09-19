using System;
namespace ValheimSagas;

// Pure world-anchored sampling: every tile samples one continuous shared image.
internal static class TerrainAtlasSampling {
 internal static byte[] Tile(byte[] atlas,int size,float worldExtent,int cx,int cz){
  if(size<1||atlas.Length!=(long)size*size*4||worldExtent<=0||float.IsNaN(worldExtent)||float.IsInfinity(worldExtent))throw new ArgumentException("Invalid terrain atlas.");
  var result=new byte[64*64*4];
  for(int z=0;z<64;z++)for(int x=0;x<64;x++){
   float u=((cx*64f+x+.5f)/worldExtent+.5f)*size-.5f,v=((cz*64f+z+.5f)/worldExtent+.5f)*size-.5f;
   int ix=(int)Math.Floor(u),iz=(int)Math.Floor(v);float fx=u-ix,fz=v-iz;
   int x0=Math.Max(0,Math.Min(size-1,ix)),x1=Math.Max(0,Math.Min(size-1,ix+1));
   int z0=Math.Max(0,Math.Min(size-1,iz)),z1=Math.Max(0,Math.Min(size-1,iz+1));
   for(int c=0;c<4;c++){
    float bottom=atlas[(z0*size+x0)*4+c]*(1-fx)+atlas[(z0*size+x1)*4+c]*fx;
    float top=atlas[(z1*size+x0)*4+c]*(1-fx)+atlas[(z1*size+x1)*4+c]*fx;
    result[(z*64+x)*4+c]=(byte)Math.Max(0,Math.Min(255,(int)Math.Floor(bottom*(1-fz)+top*fz+.5f)));
   }
  }
  return result;
 }
}
