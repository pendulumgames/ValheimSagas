using System;
namespace ValheimSagas;

// Pure RGBA32 validation shared by runtime capture and synthetic regression tests.
internal static class RuntimeArtPixels {
 static readonly double[] LinearBytes=MakeLinearBytes();
 static double[] MakeLinearBytes(){var result=new double[256];for(int i=0;i<256;i++){double x=i/255.0;result[i]=x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}return result;}
 internal static int CountVisible(ReadOnlySpan<byte> rgba){
  int visible=0;for(int i=0;i+3<rgba.Length;i+=4)if(rgba[i+3]>8&&(int)rgba[i]+rgba[i+1]+rgba[i+2]>24)visible++;
  return visible;
 }
 // Diagnostic only: detects colored geometry even if a shader writes zero
 // alpha. The first pixel is the observed clear color, avoiding color-space
 // assumptions. It never weakens the alpha/body publication guard.
 internal static int CountRgbVariation(ReadOnlySpan<byte> rgba){
  if(rgba.Length<4||rgba.Length%4!=0)return 0;
  int different=0;for(int i=4;i<rgba.Length;i+=4)if(Math.Abs(rgba[i]-rgba[0])+Math.Abs(rgba[i+1]-rgba[1])+Math.Abs(rgba[i+2]-rgba[2])>12)different++;
  return different;
 }
 // Operates on straight RGB readbacks, ignoring game shader alpha entirely.
 // For a linear-light Unity project, undo sRGB encoding before subtracting and
 // unpremultiplying, then encode the resulting straight foreground RGB again.
 internal static void ReconstructMatte(Span<byte> black,ReadOnlySpan<byte> white,bool linearLight){
  if(black.Length!=white.Length||black.Length%4!=0)throw new ArgumentException("Matte readbacks must be equally sized RGBA32 buffers.");
  for(int i=0;i<black.Length;i+=4){
   double r=Decode(black[i],linearLight),g=Decode(black[i+1],linearLight),b=Decode(black[i+2],linearLight);
   double difference=Math.Max(Decode(white[i],linearLight)-r,Math.Max(Decode(white[i+1],linearLight)-g,Decode(white[i+2],linearLight)-b));
   // Additive particles may change black RGB while leaving one white channel
   // unchanged. Preserve their emitted light in this straight-alpha PNG too.
   // For ordinary source-over, each premultiplied channel is already <= alpha,
   // so this lower bound leaves normal coverage unchanged (apart from rounding).
   // A PNG cannot retain true additive blending against every web background;
   // this is the bounded source-over representation of the captured emission.
   double alpha=Math.Max(Math.Max(r,Math.Max(g,b)),Math.Max(0,Math.Min(1,1-difference)));
   if(alpha<1.0/255){black[i]=black[i+1]=black[i+2]=black[i+3]=0;continue;}
   black[i]=Encode(r/alpha,linearLight);black[i+1]=Encode(g/alpha,linearLight);black[i+2]=Encode(b/alpha,linearLight);black[i+3]=(byte)Math.Round(alpha*255);
  }
 }
 static double Decode(byte value,bool linearLight)=>linearLight?LinearBytes[value]:value/255.0;
 static byte Encode(double value,bool linearLight){double x=Math.Max(0,Math.Min(1,value));if(linearLight)x=x<=.0031308?12.92*x:1.055*Math.Pow(x,1/2.4)-.055;return (byte)Math.Round(x*255);}
 // Returns a square crop in normalized viewport coordinates, preserving the
 // portrait aspect ratio. Include every materially visible pixel, including
 // cape/weapon outliers, with8% padding on each side plus readback tolerance.
 internal static bool TryPortraitFrame(ReadOnlySpan<byte> rgba,int width,int height,out float centerX,out float centerY,out float scale){
  centerX=centerY=0;scale=1;
  if(width<8||height<8||(long)width*height*4!=rgba.Length)return false;
  int left=width,right=-1,bottom=height,top=-1,count=0;
  for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(rgba[(y*width+x)*4+3]>8){
   left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);count++;
  }
  // Edge contact means the probe may already clip geometry: never zoom further.
  if(count<16||left<2||bottom<2||right>=width-2||top>=height-2||right-left<3||top-bottom<3)return false;
  scale=Math.Min(1,Math.Max(.18f,Math.Max((right-left+1)/(float)width,(top-bottom+1)/(float)height)*1.16f+4f/Math.Min(width,height)));
  if(scale>=.97f){scale=1;return false;}
  centerX=(left+right+1)/(float)width-1;centerY=(bottom+top+1)/(float)height-1;
  return true;
 }
 internal static bool HasVisibleContent(ReadOnlySpan<byte> rgba)=>rgba.Length>=4&&rgba.Length%4==0&&CountVisible(rgba)>=Math.Max(16,rgba.Length/4/1000);
 internal static bool CanPublishPortrait(bool bodyVerified,ReadOnlySpan<byte> rgba)=>bodyVerified&&HasVisibleContent(rgba);
}
