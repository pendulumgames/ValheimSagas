using System;
namespace ValheimSagas;
// Detached bounded strips avoid a single 64MiB allocation/copy on the game thread.
internal sealed class StripAtlas {
 readonly byte[][] strips;internal readonly int Size,Rows;int next,visible;internal bool Complete=>next==strips.Length;internal bool Visible{get;private set;}
 internal StripAtlas(int size,int rows){if(size<1||size>4096||rows<1||size%rows!=0)throw new ArgumentException("Invalid atlas strip shape.");Size=size;Rows=rows;strips=new byte[size/rows][];}
 internal void Add(int row,byte[] bytes){if(Complete||row!=next*Rows||bytes.Length!=Size*Rows*4)throw new ArgumentException("Out-of-order or malformed atlas strip.");strips[next++]=bytes;if(!Visible)for(int i=0;i<bytes.Length;i+=4)if(bytes[i+3]>8&&bytes[i]+bytes[i+1]+bytes[i+2]>24&&++visible>=Math.Max(16,Size*Size/1000)){Visible=true;break;}}
 internal byte[] Tile(float extent,int x,int z){if(!Complete)throw new InvalidOperationException("Atlas is still preparing.");if(extent<=0||float.IsNaN(extent)||float.IsInfinity(extent))throw new ArgumentException("Invalid world extent.");return TerrainAtlasSampling.Tile(i=>strips[i/(Size*Rows*4)][i%(Size*Rows*4)],Size,extent,x,z);}
}
