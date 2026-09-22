using System.Collections.Generic;
namespace ValheimSagas;
// Only Sagas' obsolete inventory keys are ours to remove. Never clear custom data.
public static class LootTrackingMetadata {
 public const string LegacyOrigin="sagas.origin", LegacySource="sagas.source";
 public const string GroundOrigin="sagas.dropOrigin", GroundSource="sagas.dropSource";
 public static bool RemoveLegacy(IDictionary<string,string>? data) {
  if(data==null)return false;
  bool changed=data.Remove(LegacyOrigin);return data.Remove(LegacySource)||changed;
 }
}
