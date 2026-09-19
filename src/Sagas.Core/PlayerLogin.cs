using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LiteDB;
using Newtonsoft.Json;

namespace ValheimSagas;
public sealed class PlayerLoginIdentity {
 public string World {get;set;}=""; public string PlayerId {get;set;}="";
}
public sealed partial class SagaStore {
 void RecordBossReceipt(SagaEvent e){
  if(e.Kind!="kill"||!e.Boss)return;
  var prefab=e.Prefab.EndsWith("(Clone)",StringComparison.Ordinal)?e.Prefab.Substring(0,e.Prefab.Length-7).Trim():e.Prefab;
  var boss=new[]{"Eikthyr","gd_king","Bonemass","Dragon","GoblinKing","SeekerQueen","Fader"}.FirstOrDefault(x=>x.Equals(prefab,StringComparison.OrdinalIgnoreCase));if(boss==null)return;
  foreach(var id in e.Contributors.Concat(new[]{e.PlayerId}).Where(x=>!string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal))
   db.GetCollection("bossReceipts").Upsert(new BsonDocument{{"_id",Key(e.World,id,boss)},{"owner",Key(e.World,id)},{"boss",boss}});
 }
 public IReadOnlyList<string> CompletedBossKeys(string world,string player){lock(gate)return db.GetCollection("bossReceipts").Find(Query.EQ("owner",Key(world,player))).Select(row=>row["boss"].AsString).ToArray();}
 public int CompletedBossCount(string world,string player){lock(gate)return db.GetCollection("bossReceipts").Count(Query.EQ("owner",Key(world,player)));}

 // Only the authenticated game connection may call issuance. Store a high-entropy
 // token hash, never the credential itself. One active credential per character/world.
 public string RotatePlayerLogin(string world,string player){lock(gate){
  if(!Players(world).Any(p=>p.PlayerId==player))throw new ArgumentException("Character not recorded yet.");
  var bytes=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);
  var token="sagas_"+BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
  db.GetCollection("playerLogins").Upsert(new BsonDocument{{"_id",Key(world,player)},{"world",world},{"player",player},{"hash",LoginHash(token)}});return token;
 }}
 public void RevokePlayerLogin(string world,string player){lock(gate)db.GetCollection("playerLogins").Delete(Key(world,player));}
 static string LoginHash(string token){using var hash=SHA256.Create();return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(token)));}
 public PlayerLoginIdentity? AuthenticatePlayer(string token){
  if(token==null||token.Length!=70||!token.StartsWith("sagas_",StringComparison.Ordinal)||token.Skip(6).Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return null;
  lock(gate){var row=db.GetCollection("playerLogins").FindOne(Query.EQ("hash",LoginHash(token)));return row==null?null:new PlayerLoginIdentity{World=row["world"].AsString,PlayerId=row["player"].AsString};}
 }
 public string BackgroundPreference(string world,string player){lock(gate){var row=db.GetCollection("backgroundPreferences").FindById(Key(world,player));return row==null?"automatic":row["biome"].AsString;}}
 public void SetBackgroundPreference(string world,string player,string biome){lock(gate)db.GetCollection("backgroundPreferences").Upsert(new BsonDocument{{"_id",Key(world,player)},{"biome",biome}});}
}
public sealed partial class SagaService {
 static readonly Dictionary<string,string> BackgroundBiomes=new Dictionary<string,string>(StringComparer.Ordinal){
  {"meadows","Meadows"},{"black-forest","BlackForest"},{"swamp","Swamp"},{"mountain","Mountain"},{"plains","Plains"},{"mistlands","Mistlands"},{"ashlands","Ashlands"},{"deep-north","DeepNorth"}
 };
 static readonly string[] BackgroundOrder={"meadows","black-forest","swamp","mountain","plains","mistlands","ashlands","deep-north"};
 int CompletedBosses(string world,string player)=>store.CompletedBossCount(world,player);
 int BackgroundProgress(string world,string player){var receipts=store.CompletedBossKeys(world,player);return KnownBosses.Where(b=>receipts.Contains(b.Key)).Select(b=>b.Order).DefaultIfEmpty(0).Max();}
 string[] AllowedBackgrounds(string world,string player)=>new[]{"automatic"}.Concat(BackgroundOrder.Take(BackgroundProgress(world,player)+1)).ToArray();
 object LoginSession(string world,PlayerLoginIdentity? identity){
  world=identity?.World??world;
  var player=identity!=null?store.Players(world).FirstOrDefault(p=>p.PlayerId==identity.PlayerId):null;
  var count=player==null?0:CompletedBosses(world,player.PlayerId);
  return new{world=identity?.World,playerId=player?.PlayerId,name=player?.Name,backgroundUnlocked=player!=null&&player.ShareProfile,backgroundPreference=player==null?"automatic":store.BackgroundPreference(world,player.PlayerId),completedBosses=count,progressionTier=player==null?0:BackgroundProgress(world,player.PlayerId),allowedBackgrounds=player==null||!player.ShareProfile?Array.Empty<string>():AllowedBackgrounds(world,player.PlayerId),requiredBosses=7};
 }
 sealed class BackgroundRequest {public string World {get;set;}="";public string Biome {get;set;}="";}
 async Task SaveBackground(HttpListenerContext c,PlayerLoginIdentity? identity){
  if(identity==null){await Respond(c,403,new{error="Log in with your personal Viking token to customize your own background."});return;}
  if(c.Request.ContentLength64<1||c.Request.ContentLength64>1024||!(c.Request.ContentType??"").StartsWith("application/json",StringComparison.OrdinalIgnoreCase)){await Respond(c,400,new{error="A bounded JSON request is required."});return;}
  BackgroundRequest? request;
  try{using var reader=new StreamReader(c.Request.InputStream,Encoding.UTF8);var read=reader.ReadToEndAsync();if(await Task.WhenAny(read,Task.Delay(5000))!=read){await Respond(c,408,new{error="Request timed out."});return;}request=JsonConvert.DeserializeObject<BackgroundRequest>(await read,new JsonSerializerSettings{MaxDepth=4,TypeNameHandling=TypeNameHandling.None,MissingMemberHandling=MissingMemberHandling.Error});}
  catch(JsonException){await Respond(c,400,new{error="Invalid background request."});return;}
  if(request==null||request.World!=identity.World||request.Biome==null||request.Biome!="automatic"&&!BackgroundBiomes.ContainsKey(request.Biome)){await Respond(c,400,new{error="Choose a valid background for your logged-in world."});return;}
  var player=store.Players(identity.World).FirstOrDefault(p=>p.PlayerId==identity.PlayerId);
  if(player==null||!player.ShareProfile||!AllowedBackgrounds(identity.World,identity.PlayerId).Contains(request.Biome)){await Respond(c,403,new{error="Choose scenery up to your highest recorded boss victory. Profile sharing must be enabled; team kill credit counts."});return;}
  store.SetBackgroundPreference(identity.World,identity.PlayerId,request.Biome);
  await Respond(c,200,LoginSession(identity.World,identity));
 }
}
