using System.Collections;
using System.Diagnostics;
using ValheimSagas;

// Synthetic fog only: no game execution, saves, or terrain textures needed.
int checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
bool HasKnown(BitArray bits,int size,float pixel,int cx,int cz){
 for(int z=0;z<16;z++)for(int x=0;x<16;x++){
  float wx=cx*64+x*4,wz=cz*64+z*4;
  int minx=(int)MathF.Floor(wx/pixel+size/2f),minz=(int)MathF.Floor(wz/pixel+size/2f);
  int maxx=(int)MathF.Ceiling((wx+4)/pixel+size/2f),maxz=(int)MathF.Ceiling((wz+4)/pixel+size/2f);
  if(minx<0||minz<0||maxx>=size||maxz>=size)continue;
  bool known=true;
  for(int pz=minz;pz<=maxz&&known;pz++)for(int px=minx;px<=maxx;px++)if(!bits[pz*size+px]){known=false;break;}
  if(known)return true;
 }
 return false;
}
var random=new Random(2187);
foreach(float pixel in new[]{.5f,1f,3f,4f,8f,12f,64f,128f})foreach(int size in new[]{64,128,256}){
 var bits=new BitArray(size*size);
 for(int trial=0;trial<120;trial++){
  bits.SetAll(false);
  int radius=(int)Math.Ceiling(size*pixel/128)+2,cx=random.Next(-radius,radius),cz=random.Next(-radius,radius);
  Check(!RuntimeTerrainMask.MayContainKnownSquare(bits,size,pixel,cx,cz),"empty map rejected");
  // Random rectangles include partially/fully known squares, boundaries and gaps.
  for(int rectangle=0;rectangle<10;rectangle++){
   int x=random.Next(size),z=random.Next(size),w=random.Next(1,24),h=random.Next(1,24);
   for(int rz=z;rz<Math.Min(size,z+h);rz++)for(int rx=x;rx<Math.Min(size,x+w);rx++)bits[rz*size+rx]=true;
  }
  bool baseline=HasKnown(bits,size,pixel,cx,cz);
  Check(!baseline||RuntimeTerrainMask.MayContainKnownSquare(bits,size,pixel,cx,cz),"known sub-tile must never be rejected");
  bits.SetAll(true);baseline=HasKnown(bits,size,pixel,cx,cz);
  Check(!baseline||RuntimeTerrainMask.MayContainKnownSquare(bits,size,pixel,cx,cz),"fully known map boundary must never be rejected");
 }
}
Check(!RuntimeTerrainMask.MayContainKnownSquare(new BitArray(4),3,4,0,0),"short fog buffer rejected");
foreach(var invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity})Check(!RuntimeTerrainMask.MayContainKnownSquare(new BitArray(16),4,invalid,0,0),"invalid scale rejected");
// A2x2 personally explored source block covers exactly the first4x4 one-metre
// output pixels. The next source boundary must remain hidden.
var personal=new BitArray(128*128);for(int z=64;z<=65;z++)for(int x=64;x<=65;x++)personal[z*128+x]=true;
int visible=0;for(int z=0;z<64;z++)for(int x=0;x<64;x++)if(RuntimeTerrainMask.SquareKnown(personal,128,4,x,z,1))visible++;
Check(visible==16,"one-metre output preserves conservative personal fog exactly");
Check(RuntimeTerrainMask.MayContainKnownSquare(personal,128,4,0,0,64),"high-resolution precheck retains small known region");
personal.SetAll(false);
Check(!RuntimeTerrainMask.SquareKnown(personal,128,4,0,0,1),"other players exploration cannot grant access through empty personal mask");
// Fine custom maps can reveal1m without revealing any4m corner, so the64-grid
// early reject must use64mandatory corners rather than the legacy16-grid.
for(int z=66;z<=67;z++)for(int x=66;x<=67;x++)personal[z*128+x]=true;
Check(RuntimeTerrainMask.SquareKnown(personal,128,1,2,2,1),"one-metre known island exists");
Check(RuntimeTerrainMask.MayContainKnownSquare(personal,128,1,0,0,64),"fine-map known island is not falsely rejected");
foreach(float pixel in new[]{.5f,1f,3f,4f,8f,12f,64f,128f}){
 var fog=new BitArray(128*128);
 for(int trial=0;trial<120;trial++){
  fog.SetAll(false);int px=random.Next(120),pz=random.Next(120);
  for(int z=pz;z<pz+8;z++)for(int x=px;x<px+8;x++)fog[z*128+x]=true;
  int cx=(int)Math.Floor((px-64)*pixel/64),cz=(int)Math.Floor((pz-64)*pixel/64);bool any=false;
  for(int z=0;z<64&&!any;z++)for(int x=0;x<64;x++)if(RuntimeTerrainMask.SquareKnown(fog,128,pixel,cx*64+x,cz*64+z,1)){any=true;break;}
  Check(!any||RuntimeTerrainMask.MayContainKnownSquare(fog,128,pixel,cx,cz,64),"64-grid randomized precheck has no false rejection");
 }
}
var empty=new BitArray(2048*2048);var sw=Stopwatch.StartNew();
// Synthetic atlas with distinguishable X/Z gradients. Shared-world sampling
// must preserve orientation, exact centres and continuity across every seam.
var atlas=new byte[128*128*4];for(int z=0;z<128;z++)for(int x=0;x<128;x++){int i=(z*128+x)*4;atlas[i]=(byte)x;atlas[i+1]=(byte)z;atlas[i+3]=255;}
var southwest=TerrainAtlasSampling.Tile(atlas,128,128,-1,-1);var southeast=TerrainAtlasSampling.Tile(atlas,128,128,0,-1);var northwest=TerrainAtlasSampling.Tile(atlas,128,128,-1,0);
for(int z=0;z<64;z++)for(int x=0;x<64;x++){
 int i=(z*64+x)*4;
 Check(southwest[i]==x&&southwest[i+1]==z&&southwest[i+3]==255,"negative-world tile preserves exact atlas pixel centres");
 Check(southeast[i]==x+64&&southeast[i+1]==z,"east tile uses contiguous atlas coordinates");
 Check(northwest[i]==x&&northwest[i+1]==z+64,"north tile is not vertically flipped");
}
var repeated=TerrainAtlasSampling.Tile(atlas,128,128,0,-1);
Check(repeated.SequenceEqual(southeast),"capture order and time cannot change extracted atlas pixels");
var enlargedLeft=TerrainAtlasSampling.Tile(atlas,128,256,-1,0);var enlargedRight=TerrainAtlasSampling.Tile(atlas,128,256,0,0);
for(int z=0;z<64;z++)Check(Math.Abs(enlargedRight[z*64*4]-enlargedLeft[(z*64+63)*4])<=1,"bilinear upscale stays continuous across64m edge");
bool invalidAtlas=false;try{TerrainAtlasSampling.Tile(new byte[4],128,128,0,0);}catch(ArgumentException){invalidAtlas=true;}Check(invalidAtlas,"invalid atlas rejected");
sw.Restart();
for(int i=0;i<16000;i++)HasKnown(empty,2048,12,i%40-20,i/40%40-20);
double baselineMs=sw.Elapsed.TotalMilliseconds;sw.Restart();
for(int i=0;i<16000;i++)RuntimeTerrainMask.MayContainKnownSquare(empty,2048,12,i%40-20,i/40%40-20);
Console.WriteLine($"PASS {checks} synthetic fog and coherent-atlas checks. Empty-cell16000 reference={baselineMs:F2}ms precheck={sw.Elapsed.TotalMilliseconds:F2}ms (development runtime only).");
