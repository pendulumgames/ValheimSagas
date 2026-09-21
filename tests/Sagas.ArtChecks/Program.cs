using ValheimSagas;
using System.Numerics;
int checks=0;void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
var policy=new PortraitRefresh();
Check(!policy.Due("armor-a",0)&&!policy.Due("armor-a",1),"First appearance settles before capture");
Check(policy.Due("armor-a",2),"Settled initial appearance captures");policy.Started(2);policy.Completed("armor-a",3);
for(int second=4;second<603;second++)Check(!policy.Due("armor-a",second),"Unchanged appearance has no minute-by-minute refresh");
Check(!policy.Due("armor-a",603)&&!policy.Due("armor-a",86400),"No periodic capture even after a day unchanged");
Check(!policy.Due("armor-b",86401)&&policy.Due("armor-b",86403),"Equipment change remains eligible");policy.Started(86403);policy.Completed("armor-b",86404);
Check(!policy.Due("armor-c",86405)&&!policy.Due("armor-d",86406)&&!policy.Due("armor-d",86408),"Rapid swaps debounce and obey cooldown");Check(policy.Due("armor-d",86413),"Latest settled outfit captures after cooldown");policy.Started(86413);policy.Failed(86414);Check(!policy.Due("armor-d",86473)&&policy.Due("armor-d",86474),"Failure retries stay bounded");
policy.Reset();Check(!policy.Due("seated",0,false)&&!policy.Due("seated",60,false),"Initial seated pose is never captured");Check(!policy.Due("seated",61)&&!policy.Due("seated",62)&&policy.Due("seated",63),"Standing restarts settle delay");policy.Started(63);policy.Completed("seated",64);
Check(!policy.Due("new-weapon",65,false)&&!policy.Due("new-weapon",120,false),"Gear change while seated stays pending");Check(!policy.Due("new-weapon",121)&&policy.Due("new-weapon",123),"Pending weapon captures after standing");
policy.Defer();Check(!policy.Due("new-weapon",130)&&policy.Due("new-weapon",132),"Mid-capture seated cancellation restarts debounce");
policy.Reset();Check(!policy.Due("new-world",700)&&policy.Due("new-world",702),"World/privacy reset forgets prior portrait");
foreach(var cancelAt in new[]{0,1,2}){
 var lease=new ReadbackLifetime();int released=0;lease.Begin();lease.Begin();
 for(int i=0;i<cancelAt;i++)lease.Complete();lease.Retire(()=>released++);Check(released==(cancelAt==2?1:0),"Retirement never releases an in-flight GPU lease");
 for(int i=cancelAt;i<2;i++){lease.Complete();Check(released==(i==1?1:0),"Release occurs only after the last callback");}
 lease.Retire(()=>released++);Check(released==1,"Cancellation/cleanup is idempotent");try{lease.Begin();Check(false,"Retired capture cannot submit");}catch(InvalidOperationException){Check(true,"Retired submission rejected");}
}
var partialLease=new ReadbackLifetime();int partialRelease=0;partialLease.Begin();partialLease.Retire(()=>partialRelease++);Check(partialRelease==0,"Second-submission failure retains first GPU request");partialLease.Complete();Check(partialRelease==1,"Partial submission releases after callback");
var emptyLease=new ReadbackLifetime();int emptyRelease=0;emptyLease.Retire(()=>emptyRelease++);Check(emptyRelease==1,"Failure before submission releases immediately");
try{emptyLease.Complete();Check(false,"Unexpected callback rejected");}catch(InvalidOperationException){Check(true,"Unbalanced completion rejected");}
var workTiming=new PortraitWorkTiming();workTiming.Add(10,1.5);workTiming.Add(10,2.5);workTiming.Add(11,.5);Check(workTiming.Maximum==4,"Capture timing sums preparation and callback work in the same frame");workTiming.Reset();Check(workTiming.Maximum==0,"New capture has independent frame timing");
var sample=new byte[]{255,0,0,255,0,255,0,0,255,0,0,255,0,0,255,0};
var small=RuntimeArtPixels.ResizePortrait(sample,2,2,1,1);Check(small.SequenceEqual(new byte[]{255,0,0,127}),"Oversize downsample preserves foreground color without transparent fringes");
Check(RuntimeArtPixels.ResizePortrait(sample,2,2,2,2).Where((v,i)=>sample[i+3-i%4]>0).SequenceEqual(sample.Where((v,i)=>sample[i+3-i%4]>0)),"Same-size fallback preserves visible pixels");
try{RuntimeArtPixels.ResizePortrait(sample,2,2,3,3);Check(false,"Upscaling rejected");}catch(ArgumentException){Check(true,"Fallback never upscales");}
// Synthetic reconstruction of observed failure: all98304pixels were(9,14,17,0).
var rgba=new byte[256*384*4];for(int i=0;i<rgba.Length;i+=4){rgba[i]=9;rgba[i+1]=14;rgba[i+2]=17;}
Check(!RuntimeArtPixels.HasVisibleContent(rgba),"observed transparent clear-only portrait is rejected");
for(int i=0;i<rgba.Length;i+=4){rgba[i]=255;rgba[i+1]=255;rgba[i+2]=255;}
Check(!RuntimeArtPixels.HasVisibleContent(rgba),"bright RGB under zero alpha is invisible");
for(int i=0;i<20*4;i+=4)rgba[i+3]=255;
Check(!RuntimeArtPixels.HasVisibleContent(rgba),"tiny pixel artifact cannot pass for a full portrait");
for(int i=0;i<4000*4;i+=4)rgba[i+3]=255;
Check(RuntimeArtPixels.HasVisibleContent(rgba),"visible body-sized synthetic geometry is accepted");
Check(!RuntimeArtPixels.CanPublishPortrait(false,rgba),"visible floating weapon cannot publish without isolated body verification");
Check(RuntimeArtPixels.CanPublishPortrait(true,rgba),"verified body with visible full image may publish");
Check(RuntimeArtPixels.CountVisible(rgba)==4000,"opaque visible pixels counted");
Array.Clear(rgba);for(int i=0;i<rgba.Length;i+=4)rgba[i+3]=255;
Check(!RuntimeArtPixels.HasVisibleContent(rgba),"opaque black rendering failure is rejected");
Check(!RuntimeArtPixels.CanPublishPortrait(true,rgba),"body verification cannot permit blank full portrait");
Check(!RuntimeArtPixels.HasVisibleContent(new byte[3]),"malformed pixel buffer rejected");
Check(!RuntimeArtPixels.HasVisibleContent(Array.Empty<byte>()),"empty capture rejected");
Check(PortraitGeometry.PlausibleSize(2.4f,2.7f),"baked pose variation is valid");
Check(!PortraitGeometry.PlausibleSize(2.4f,220),"observed nearly200m character-scale regression is rejected");
Check(!PortraitGeometry.PlausibleSize(2.4f,.024f),"inverse100x importer-scale error is rejected");
Check(!PortraitGeometry.PlausibleSize(0,1)&&!PortraitGeometry.PlausibleSize(1,float.NaN)&&!PortraitGeometry.PlausibleSize(1,float.PositiveInfinity),"invalid mesh bounds cannot frame a camera");
Check(!PortraitGeometry.PreferRigidBake(3.16f,.02f*95,.02f),"observed body requires compensated bake plus95x renderer matrix");
Check(!PortraitGeometry.PreferRigidBake(3.52f,.09f*16.17f,.09f),"observed skinned armor keeps importer-scale compensation");
Check(!PortraitGeometry.PreferRigidBake(1.01f,.01f*100,.01f),"observed hair requires100x renderer matrix");
Check(PortraitGeometry.PreferRigidBake(2,200,2),"world-sized alternative bake cannot have scale applied twice");
Check(!PortraitGeometry.PreferRigidBake(2,2,2.05f),"equal candidates prefer the complete renderer matrix");
foreach(var extent in new[]{new Vector3(.5f,1,.3f),new Vector3(2,1,.7f),new Vector3(.4f,2,.8f),new Vector3(1,1,1)}){
 float distance=PortraitGeometry.CameraDistance(extent.X,extent.Y,extent.Z,2f/3f,12);
 var view=Matrix4x4.CreateLookAt(Vector3.Normalize(new Vector3(.2f,0,1))*distance,Vector3.Zero,Vector3.UnitY);
 var projection=Matrix4x4.CreatePerspectiveFieldOfView(12*MathF.PI/180,2f/3f,.05f,distance+extent.Length()*2+10);
 for(int c=0;c<8;c++){
  var point=new Vector4(extent.X*((c&1)==0?-1:1),extent.Y*((c&2)==0?-1:1),extent.Z*((c&4)==0?-1:1),1);
  var clip=Vector4.Transform(point,view*projection);
  Check(clip.W>0&&Math.Abs(clip.X/clip.W)<1&&Math.Abs(clip.Y/clip.W)<1&&clip.Z/clip.W>=0&&clip.Z/clip.W<=1,"independent projection keeps equipped mesh bounds inside camera");
 }
}
// Real source hierarchy now remains in world space; verify a full orbit fits
// its axis-aligned bounds, including wide equipped weapons and negative yaw.
foreach(var extent in new[]{new Vector3(.5f,1.7f,.3f),new Vector3(2,1,.7f),new Vector3(.4f,2,.8f)})
for(int yaw=-180;yaw<180;yaw+=15){
 float distance=PortraitGeometry.RotatingCameraDistance(extent.X,extent.Y,extent.Z,2f/3f,12);
 var direction=Vector3.Transform(Vector3.Normalize(new Vector3(.2f,0,1)),Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw*MathF.PI/180));
 var view=Matrix4x4.CreateLookAt(direction*distance,Vector3.Zero,Vector3.UnitY);
 var projection=Matrix4x4.CreatePerspectiveFieldOfView(12*MathF.PI/180,2f/3f,.05f,distance+extent.Length()*2+10);
 for(int c=0;c<8;c++){
  var point=new Vector4(extent.X*((c&1)==0?-1:1),extent.Y*((c&2)==0?-1:1),extent.Z*((c&4)==0?-1:1),1);
  var clip=Vector4.Transform(point,view*projection);
  Check(clip.W>0&&Math.Abs(clip.X/clip.W)<1&&Math.Abs(clip.Y/clip.W)<1&&clip.Z/clip.W>=0&&clip.Z/clip.W<=1,"unmodified world-space rig fits camera at every tested yaw");
 }
}
var alphaDiagnostic=new byte[]{9,14,17,0,9,14,17,0,200,80,20,0};
Check(RuntimeArtPixels.CountRgbVariation(alphaDiagnostic)==1,"zero-alpha colored geometry is distinguished from a cleared target");
Check(RuntimeArtPixels.CountVisible(alphaDiagnostic)==0,"zero-alpha diagnostic does not bypass visibility guard");
Check(RuntimeArtPixels.CountRgbVariation(new byte[]{53,66,72,0,53,66,72,0})==0,"clear color-space conversion is not mistaken for geometry");
Check(RuntimeArtPixels.CountRgbVariation(Array.Empty<byte>())==0,"empty diagnostic buffer is safe");
// Reconstruct the observed incident shape: a few thousand colored body pixels
// with zero shader alpha. Cleared background must stay transparent, and body
// verification must still be mandatory after recovering those pixels.
var blackPass=new byte[256*384*4];var whitePass=new byte[blackPass.Length];
for(int i=0;i<whitePass.Length;i+=4){whitePass[i]=whitePass[i+1]=whitePass[i+2]=255;}
for(int n=1000;n<3730;n++){
 int i=n*4;blackPass[i]=whitePass[i]=160;blackPass[i+1]=whitePass[i+1]=100;blackPass[i+2]=whitePass[i+2]=60;
}
Check(RuntimeArtPixels.CountVisible(blackPass)==0,"incident body pixels are invisible before alpha reconstruction");
RuntimeArtPixels.ReconstructMatte(blackPass,whitePass,false);
Check(RuntimeArtPixels.CountVisible(blackPass)==2730,"observed zero-alpha body pixel count is recovered");
Check(blackPass[0]==0&&blackPass[3]==0&&blackPass[4003]==255,"background remains transparent and body becomes opaque");
Check(!RuntimeArtPixels.CanPublishPortrait(false,blackPass),"reconstructed equipment still cannot bypass body verification");
Check(RuntimeArtPixels.CanPublishPortrait(true,blackPass),"verified reconstructed body is publishable");
// Generate independent source-over composites and verify straight-color/alpha
// recovery in both gamma and linear-light rendering. Quantized readbacks allow
// a small error on low-alpha dark pixels; opaque geometry recovers exactly.
static byte Store(double x,bool linear)=> (byte)Math.Round(255*(linear?(x<=.0031308?12.92*x:1.055*Math.Pow(x,1/2.4)-.055):x));
foreach(bool linear in new[]{false,true})
foreach(double opacity in new[]{0,.1,.25,.5,.8,1})
foreach(double color in new[]{0,.02,.2,.5,.8,1}){
 byte cb=Store(color*opacity,linear),cw=Store(color*opacity+1-opacity,linear);
 var overBlack=new byte[]{cb,cb,cb,0};var overWhite=new byte[]{cw,cw,cw,0};
 RuntimeArtPixels.ReconstructMatte(overBlack,overWhite,linear);
 Check(Math.Abs(overBlack[3]-Math.Round(opacity*255))<=2,"background difference recovers source-over opacity");
 Check(opacity==0?overBlack[0]==0:Math.Abs(overBlack[0]-Store(color,linear))<=8,"matte unpremultiplication preserves foreground color");
}
var cleared=new byte[400];var whiteClear=new byte[400];for(int i=0;i<400;i+=4)whiteClear[i]=whiteClear[i+1]=whiteClear[i+2]=255;
RuntimeArtPixels.ReconstructMatte(cleared,whiteClear,true);
Check(!RuntimeArtPixels.HasVisibleContent(cleared),"empty two-background render remains rejected");
bool badMatte=false;try{RuntimeArtPixels.ReconstructMatte(new byte[4],new byte[8],false);}catch(ArgumentException){badMatte=true;}
Check(badMatte,"mismatched readbacks are rejected");
// Match the observed mostly-empty256x384 portrait, with its measured96x94 alpha bounds.
static byte[] Silhouette(int w,int h,int left,int bottom,int right,int top){
 var pixels=new byte[w*h*4];for(int y=bottom;y<=top;y++)for(int x=left;x<=right;x++)pixels[(y*w+x)*4+3]=255;return pixels;
}
var tinyCharacter=Silhouette(256,384,88,141,183,234);
Check(RuntimeArtPixels.TryPortraitFrame(tinyCharacter,256,384,out float cropX,out float cropY,out float cropScale),"observed small character gets a tighter projection");
Check(1/cropScale>2.2&&94/cropScale*511/384>275,"rerender materially increases actual character pixel height");
foreach(var shape in new[]{(88,145,182,239),(25,80,180,310),(110,20,140,360),(30,110,230,170)}){
 var pixels=Silhouette(256,384,shape.Item1,shape.Item2,shape.Item3,shape.Item4);
 bool reframed=RuntimeArtPixels.TryPortraitFrame(pixels,256,384,out float cx,out float cy,out float fraction);
 for(int corner=0;corner<4;corner++){
  double px=((corner&1)==0?shape.Item1:shape.Item3)+.5,py=((corner&2)==0?shape.Item2:shape.Item4)+.5;
  // Project the same screen-space silhouette at several depths; unlike moving
  // the camera forward, this crop must preserve both close weapons and cape.
  foreach(float depth in new[]{1f,4f,30f,100f}){
   var clip=new Vector4((float)(px/256*2-1)*depth,(float)(py/384*2-1)*depth,.5f*depth,depth);
   float x=(clip.X-cx*clip.W)/fraction,y=(clip.Y-cy*clip.W)/fraction;
   Check(Math.Abs(x/clip.W)<1&&Math.Abs(y/clip.W)<1,"padded silhouette corners remain unclipped at every depth");
  }
 }
}
Check(!RuntimeArtPixels.TryPortraitFrame(Silhouette(256,384,0,100,90,200),256,384,out _,out _,out _),"edge-touching geometry is never enlarged into further clipping");
Check(!RuntimeArtPixels.TryPortraitFrame(new byte[256*384*4],256,384,out _,out _,out _),"blank capture cannot drive framing");
Check(!RuntimeArtPixels.TryPortraitFrame(new byte[4],256,384,out _,out _,out _),"mismatched frame dimensions are rejected");
var bowOutlier=Silhouette(256,384,100,130,145,250);bowOutlier[(190*256+210)*4+3]=255;
Check(RuntimeArtPixels.TryPortraitFrame(bowOutlier,256,384,out cropX,out cropY,out cropScale)&&((210.5/256*2-1)-cropX)/cropScale<.9,"isolated visible bow tip is included with padding");
// Emissive/additive particles can leave a white background channel untouched;
// ordinary difference-only matting erased colored weapon glow entirely.
foreach(bool linear in new[]{false,true})
foreach(var glow in new[]{(.5,0.0,0.0),(0.0,.2,.8),(.1,.4,.05),(1.0,0.0,1.0)}){
 var emitted=new byte[]{Store(glow.Item1,linear),Store(glow.Item2,linear),Store(glow.Item3,linear),0};
 var white=new byte[]{255,255,255,0};
 RuntimeArtPixels.ReconstructMatte(emitted,white,linear);
 Check(emitted[3]>0,"colored additive effect survives transparent PNG reconstruction");
 static double DecodeTest(byte value,bool lin){double x=value/255.0;return lin?(x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4)):x;}
 var expected=new[]{glow.Item1,glow.Item2,glow.Item3};
 for(int channel=0;channel<3;channel++)Check(Math.Abs(DecodeTest(emitted[channel],linear)*emitted[3]/255.0-expected[channel])<.012,"PNG preserves frozen emission contribution over black");
 Check(!RuntimeArtPixels.CanPublishPortrait(false,emitted),"emission cannot replace the required body verification");
}
RuntimeArtPixels.TryPortraitFrame(tinyCharacter,256,384,out _,out _,out var detailScale);
Check(94/detailScale*1536/384>825,"high-resolution rerender supplies over825 actual character pixels for the reported silhouette");
// Fault-injected render boundaries exercise the production undo journal. Each
// matte render must restore original masks/globals even if setup or GPU render
// fails; no simulated scene mutation is allowed to leak into the next frame.
foreach(int failureAt in new[]{-1,0,1,2,3,4}){
 var scene=new[]{-1,0x1234,17,88};var original=scene.ToArray();var applied=0;
 try{
  using(var scope=new TemporaryRenderState()){
   for(int i=0;i<scene.Length;i++){
    int slot=i;scope.Set(()=>scene[slot],value=>{scene[slot]=value;if(value==0&&failureAt==slot)throw new InvalidOperationException("native setter failure after mutation");},0);applied++;
   }
   Check(scene.All(value=>value==0),"neutral render receives overridden scene state");
   if(failureAt==4)throw new InvalidOperationException("GPU render failure");
  }
 }catch(InvalidOperationException){Check(failureAt>=0,"only requested fault is thrown");}
 Check(scene.SequenceEqual(original),"all scene state restored on success and injected setup/render failure");
}
var restoreOrder=new List<int>();var restoreScope=new TemporaryRenderState();
restoreScope.Remember(()=>restoreOrder.Add(1));
restoreScope.Remember(()=>{restoreOrder.Add(2);throw new InvalidOperationException("one native restore failed");});
restoreScope.Remember(()=>restoreOrder.Add(3));
try{restoreScope.Dispose();Check(false,"native restore error is reported");}catch(AggregateException ex){Check(ex.InnerExceptions.Count==1,"restore failure remains observable");}
Check(restoreOrder.SequenceEqual(new[]{3,2,1}),"one restore failure cannot skip other scene restoration");
restoreScope.Dispose();Check(restoreOrder.Count==3,"dispose is idempotent after restoration failure");
try{restoreScope.Remember(()=>{});Check(false,"disposed scopes reject new scene mutations");}catch(ObjectDisposedException){Check(true,"disposed scope cannot leak new mutations");}
Console.WriteLine($"PASS {checks} synthetic portrait alpha reconstruction, emission, visibility, scale, camera-framing and render-state restoration checks. This does not validate GPU rendering.");
