using System.Collections.Generic;
using System.Linq;
using LiteDB;
namespace ValheimSagas;
public sealed partial class SagaStore {
 public void WorldName(string world,string name){lock(gate)db.GetCollection("worldNames").Upsert(new BsonDocument{{"_id",world},{"name",name}});}
 public Dictionary<string,string> WorldNames(){lock(gate)return db.GetCollection("worldNames").FindAll().ToDictionary(d=>d["_id"].AsString,d=>d["name"].AsString);}
}
