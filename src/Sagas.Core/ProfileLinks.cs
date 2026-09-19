using System;
using System.Text;
using System.Text.RegularExpressions;
using LiteDB;

namespace ValheimSagas;
public sealed partial class SagaStore {
 // Only the server assigns links. A client-supplied slug can never claim another profile.
 // Reservations survive name changes and are shared across worlds for the same player ID.
 void AssignProfileSlug(PlayerSnapshot player) {
  var links=db.GetCollection("profileLinks");var id=Key(player.PlayerId);
  var existing=links.FindById(id);
  if(existing!=null){player.ProfileSlug=existing["display"].AsString;return;}
  string name;
  try{name=(player.Name??"").Normalize(NormalizationForm.FormKC);}catch(ArgumentException){name="Viking";}
  var stem=Regex.Replace(name,@"[^\p{L}\p{Nd}]+","-").Trim('-');
  if(stem.Length>60)stem=stem.Substring(0,60).TrimEnd('-');
  if(stem.Length==0)stem="Viking";
  var slug=stem;var suffix=0;
  while(links.Exists(Query.EQ("slug",slug.ToLowerInvariant())))slug=stem+"-"+(++suffix);
  links.Insert(new BsonDocument{{"_id",id},{"slug",slug.ToLowerInvariant()},{"display",slug}});
  player.ProfileSlug=slug;
 }
}
