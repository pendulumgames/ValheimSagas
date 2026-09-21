using System;
using BepInEx.Bootstrap;
namespace ValheimSagas;
internal static class OptionalTypes {
 // Resolve inside the registered mod assembly; never enumerate every loaded game/mod type.
 internal static Type? InPlugin(string guid,string name)=>Chainloader.PluginInfos.TryGetValue(guid,out var plugin)?plugin.Instance?.GetType().Assembly.GetType(name,false):null;
}
