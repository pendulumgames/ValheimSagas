using System;
using System.Collections.Generic;
using System.Linq;
namespace ValheimSagas;
public sealed class WirePart { public string Id=""; public bool Compressed; public int Lane,Index,Count; public byte[] Data=Array.Empty<byte>(); }
public sealed class WireTransfer {
 public const int Chunk=2400, Maximum=128000;
 sealed class Entry { public string Id=""; public bool Compressed; public double Started; public byte[][] Parts=Array.Empty<byte[]>(); }
 readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
 public void Clear()=>entries.Clear();
 public byte[]? Accept(string owner,WirePart p,double now){
  foreach(var key in entries.Where(x=>now-x.Value.Started>180).Select(x=>x.Key).ToArray())entries.Remove(key);
  if(p==null||p.Lane<0||p.Lane>3||p.Id==null||p.Id.Length!=32||!Guid.TryParseExact(p.Id,"N",out _)||p.Count<1||p.Count>(Maximum+Chunk-1)/Chunk||p.Index<0||p.Index>=p.Count||p.Data==null||p.Data.Length<1||p.Data.Length>Chunk||p.Index<p.Count-1&&p.Data.Length!=Chunk)return null;
  owner=owner+":"+p.Lane;
  if(!entries.TryGetValue(owner,out var e)||e.Id!=p.Id){if(p.Index!=0||entries.Count>=128&&!entries.ContainsKey(owner))return null;entries[owner]=e=new Entry{Id=p.Id,Compressed=p.Compressed,Started=now,Parts=new byte[p.Count][]};}
  if(e.Parts.Length!=p.Count||e.Compressed!=p.Compressed)return null;e.Parts[p.Index]=p.Data;
  if(e.Parts.Any(x=>x==null))return null;entries.Remove(owner);
  int length=e.Parts.Sum(x=>x.Length);if(length>Maximum)return null;
  var result=new byte[length];int offset=0;foreach(var part in e.Parts){Array.Copy(part,0,result,offset,part.Length);offset+=part.Length;}return WireCompression.Decode(result,e.Compressed);
 }
}
