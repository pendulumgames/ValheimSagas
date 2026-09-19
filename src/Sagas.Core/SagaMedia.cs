using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LiteDB;
namespace ValheimSagas;
public sealed class MediaUpload {
 public string World {get;set;}="";public string PlayerId {get;set;}="";public string Id {get;set;}="";public string Kind {get;set;}="";public byte[] Png {get;set;}=Array.Empty<byte>();
}
public sealed partial class SagaStore {
 public void Media(MediaUpload m){lock(gate){var c=db.GetCollection("media");c.Upsert(new BsonDocument{{"_id",Key(m.World,m.PlayerId,m.Id)},{"world",m.World},{"player",m.PlayerId},{"kind",m.Kind},{"utc",DateTime.UtcNow},{"png",m.Png}});
  // Keep a bounded cache, not a permanent portrait history.
  var old=c.Find(Query.EQ("world",m.World)).Where(d=>d["player"].AsString==m.PlayerId&&d["kind"].AsString==m.Kind).OrderByDescending(d=>d["utc"].AsDateTime).Skip(m.Kind=="portrait"?2:64).ToArray();foreach(var d in old)c.Delete(d["_id"]);
 }}
 public byte[]? Media(string world,string player,string id){lock(gate)return db.GetCollection("media").FindById(Key(world,player,id))?["png"].AsBinary;}
 internal bool HasMedia(string world,string player,string id){lock(gate)return db.GetCollection("media").Exists(Query.EQ("_id",Key(world,player,id)));}
}
public sealed partial class SagaService {
 public const int MaximumPortraitBytes=1024*1024, MaximumPortraitWidth=1024, MaximumPortraitHeight=1536;
 public const int MaximumIconBytes=48*1024;
 public static bool MediaId(string s)=>s!=null&&(s==""||(s.Length==64&&s.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')));
 public static bool ValidMedia(MediaUpload m){if(m==null||!TextValid(m.World,100,true)||!TextValid(m.PlayerId,100,true)||m.Id==""||!MediaId(m.Id)||(m.Kind!="icon"&&m.Kind!="portrait")||m.Png==null||m.Png.Length<33||m.Png.Length>(m.Kind=="portrait"?MaximumPortraitBytes:MaximumIconBytes))return false;
  var b=m.Png;byte[] signature={137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82};for(int i=0;i<signature.Length;i++)if(b[i]!=signature[i])return false;
  long w=0,h=0;for(int i=16;i<20;i++)w=w*256+b[i];for(int i=20;i<24;i++)h=h*256+b[i];if(w<1||h<1||w>(m.Kind=="portrait"?MaximumPortraitWidth:512)||h>(m.Kind=="portrait"?MaximumPortraitHeight:512))return false;
  using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(b)).Replace("-","").ToLowerInvariant()==m.Id;
 }
 public bool UploadMedia(MediaUpload m,Action<bool>? committed=null){if(!ValidMedia(m))return false;var copy=Copy(m);return Enqueue(()=>{try{store.Media(copy);committed?.Invoke(true);}catch{committed?.Invoke(false);throw;}});}
 async Task ServeMedia(HttpListenerContext c,string world,string player,string id){if(!MediaId(id)||id==""||!TextValid(player,100,true)){await Respond(c,404,new{error="Artwork unavailable"});return;}
  var p=store.Players(world).FirstOrDefault(x=>x.PlayerId==player);if(p==null||!p.ShareProfile||(p.PortraitId!=id&&!p.Gear.Any(g=>g.IconId==id)&&!p.Hotbar.Any(g=>g.IconId==id))){await Respond(c,404,new{error="Artwork unavailable"});return;}
  var bytes=store.Media(world,player,id);if(bytes==null){await Respond(c,404,new{error="Artwork pending"});return;}c.Response.StatusCode=200;c.Response.ContentType="image/png";c.Response.ContentLength64=bytes.Length;await c.Response.OutputStream.WriteAsync(bytes,0,bytes.Length);c.Response.Close();
 }
}
