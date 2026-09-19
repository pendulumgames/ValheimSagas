using ValheimSagas;
using LiteDB;
using Newtonsoft.Json;
internal static class ProfileLinkChecks {
 public static void Run(string root,Action<bool,string> check){
  var dir=Path.Combine(root,"profile-links");Directory.CreateDirectory(dir);
  // Seed the pre-upgrade schema, without slugs, to exercise migration rather than only new inserts.
  using(var db=new LiteDatabase(Path.Combine(dir,"sagas.db"))){
   var p=new PlayerSnapshot{World="world",PlayerId="old",Name="Ragnarok"};
   db.GetCollection("players").Insert(new BsonDocument{{"_id","world|old"},{"world","world"},{"player","old"},{"utc",DateTime.UtcNow},{"json",JsonConvert.SerializeObject(p)}});
  }
  using(var store=new SagaStore(dir)){
   check(store.Players("world").Single().ProfileSlug=="Ragnarok","Existing profiles receive readable links on upgrade");
   void Add(string id,string name,string world="world")=>store.Player(new PlayerSnapshot{PlayerId=id,Name=name,World=world,ProfileSlug="Ragnarok"});
   Add("two","Ragnarok");Add("three","ragnarok");Add("four","Ragnarok-1");
   var rows=store.Players("world");
   check(rows.Single(p=>p.PlayerId=="two").ProfileSlug=="Ragnarok-1","Duplicate gets simple numbered suffix; forged slug ignored");
   check(rows.Single(p=>p.PlayerId=="three").ProfileSlug=="ragnarok-2","Case variants cannot claim another link");
   check(rows.Single(p=>p.PlayerId=="four").ProfileSlug=="Ragnarok-1-1","Literal numbered names do not collide with assigned suffixes");
   Add("two","Renamed");Add("two","Different world name","other");
   check(store.Players("world").Single(p=>p.PlayerId=="two").ProfileSlug=="Ragnarok-1","Rename preserves shared bookmarks");
   check(store.Players("other").Single().ProfileSlug=="Ragnarok-1","Same player across worlds retains the same reservation");
   Add("unicode","  Björn / 雪 # ?  ");Add("empty","!!!");
   check(store.Players("world").Single(p=>p.PlayerId=="unicode").ProfileSlug=="Björn-雪","Unicode names retained and unsafe URL separators removed");
   check(store.Players("world").Single(p=>p.PlayerId=="empty").ProfileSlug=="Viking","Punctuation-only names have a readable fallback");
  }
  using(var store=new SagaStore(dir)){
   check(store.Players("world").Single(p=>p.PlayerId=="two").ProfileSlug=="Ragnarok-1","Reservation survives process restart");
   store.Player(new PlayerSnapshot{World="world",PlayerId="late",Name="Ragnarok"});
   check(store.Players("world").Single(p=>p.PlayerId=="late").ProfileSlug=="Ragnarok-3","Later arrivals never renumber existing links");
  }
 }
}
