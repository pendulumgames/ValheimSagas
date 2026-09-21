using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using LiteDB;
namespace ValheimSagas;
// Choose randomly among near-best-separated colors. Never recolor established Vikings.
public static class VikingColors {
 public static bool Valid(string? color)=>color!=null&&color.Length==7&&color[0]=='#'&&color.Skip(1).All(Uri.IsHexDigit);
 static readonly string[] Palette={"#ffb454","#62d5df","#df91f0","#a5df6b","#779cff","#fa8d9c","#e9d66b","#67d9ac","#b1a1ff","#ff9368","#c3e9ef","#dcbd99","#ec83c2","#8bb3cc","#ced696","#b9c9ff","#ffcbcc","#94dacc","#c9ace0","#c4d5b6","#e9b376","#8ec7f1","#c6a46d","#b3c9d0"};
 static double[] Lab(string hex){double Channel(int offset){var s=int.Parse(hex.Substring(offset,2),NumberStyles.HexNumber)/255d;return s<=.04045?s/12.92:Math.Pow((s+.055)/1.055,2.4);}var r=Channel(1);var g=Channel(3);var b=Channel(5);double F(double t)=>t>.008856?Math.Pow(t,1d/3):7.787*t+16d/116;var x=F((r*.4124+g*.3576+b*.1805)/.95047);var y=F(r*.2126+g*.7152+b*.0722);var z=F((r*.0193+g*.1192+b*.9505)/1.08883);return new[]{116*y-16,500*(x-y),200*(y-z)};}
 public static double Distance(string a,string b){var x=Lab(a);var y=Lab(b);return Math.Sqrt(x.Zip(y,(p,q)=>(p-q)*(p-q)).Sum());}
 static string HueColor(int hue,double light){double C=(1-Math.Abs(2*light-1))*.72,h=hue/60d,x=C*(1-Math.Abs(h%2-1)),m=light-C/2;double r=0,g=0,b=0;if(h<1){r=C;g=x;}else if(h<2){r=x;g=C;}else if(h<3){g=C;b=x;}else if(h<4){g=x;b=C;}else if(h<5){r=x;b=C;}else{r=C;b=x;}return "#"+((int)Math.Round((r+m)*255)).ToString("x2")+((int)Math.Round((g+m)*255)).ToString("x2")+((int)Math.Round((b+m)*255)).ToString("x2");}
 public static string Choose(IEnumerable<string> existing){var used=existing.Where(Valid).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();var candidates=Palette.AsEnumerable();
  var ranked=candidates.Select(c=>new{Color=c,Distance=used.Length==0?100:used.Min(u=>Distance(c,u))}).ToArray();var best=ranked.Max(c=>c.Distance);
  if(best<18){candidates=candidates.Concat(Enumerable.Range(0,72).SelectMany(i=>new[]{HueColor(i*5,.62),HueColor(i*5,.78)}));ranked=candidates.Distinct().Select(c=>new{Color=c,Distance=used.Min(u=>Distance(c,u))}).ToArray();best=ranked.Max(c=>c.Distance);}
  var choices=ranked.Where(c=>c.Distance>=best-.5).Select(c=>c.Color).ToArray();using var random=RandomNumberGenerator.Create();var bytes=new byte[4];random.GetBytes(bytes);return choices[BitConverter.ToUInt32(bytes,0)%(uint)choices.Length];}

}
public sealed partial class SagaStore {
 readonly Dictionary<string,string> vikingColorCache=new Dictionary<string,string>();
 void AssignMapColor(PlayerSnapshot player){var id=Key(player.World,player.PlayerId);if(!vikingColorCache.TryGetValue(id,out var color)){var colors=db.GetCollection("vikingColors");var row=colors.FindById(id);color=row?["color"].AsString;if(!VikingColors.Valid(color)){color=VikingColors.Choose(colors.Find(Query.EQ("world",player.World)).Select(d=>d["color"].AsString));colors.Upsert(new BsonDocument{{"_id",id},{"world",player.World},{"color",color}});}vikingColorCache[id]=color!;}player.MapColor=color!;}
}
