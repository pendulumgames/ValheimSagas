using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Collections.Immutable;
var game=args.Length>0?args[0]:@"C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed";
var plugins=args.Length>1?args[1]:@"C:\Users\mecra\AppData\Roaming\com.kesomannen.gale\valheim\profiles\Test\BepInEx\plugins";
int passed=0;
void Inspect(string path,Action<MetadataReader> inspect){using var stream=File.OpenRead(path);using var pe=new PEReader(stream);inspect(pe.GetMetadataReader());}
TypeDefinition Type(MetadataReader r,string name){foreach(var h in r.TypeDefinitions){var d=r.GetTypeDefinition(h);var n=r.GetString(d.Namespace);if((n.Length==0?"":n+".")+r.GetString(d.Name)==name)return d;}throw new Exception("Missing type "+name);}
void Method(MetadataReader r,string type,string method,params string[] parameters){var matches=Type(r,type).GetMethods().Select(r.GetMethodDefinition).Where(m=>r.GetString(m.Name)==method).ToArray();if(matches.Length==0)throw new Exception($"Missing method {type}.{method}");if(parameters.Length>0&&!matches.Any(m=>parameters.All(p=>m.GetParameters().Select(r.GetParameter).Any(q=>r.GetString(q.Name)==p))))throw new Exception($"Harmony parameter mismatch {type}.{method}");passed++;}
void Field(MetadataReader r,string type,string field){if(!Type(r,type).GetFields().Select(r.GetFieldDefinition).Any(f=>r.GetString(f.Name)==field))throw new Exception($"Missing reflected field {type}.{field}");passed++;}
void Signature(MetadataReader r,string type,string method,params string[] parameters){
 var provider=new MetadataTypeNames();
 var match=Type(r,type).GetMethods().Select(r.GetMethodDefinition).Where(m=>r.GetString(m.Name)==method).Any(m=>m.DecodeSignature(provider,(object?)null).ParameterTypes.SequenceEqual(parameters));
 if(!match)throw new Exception($"Missing exact signature {type}.{method}({string.Join(",",parameters)})");passed++;
}
void TypedField(MetadataReader r,string type,string field,string expected){
 var definition=Type(r,type).GetFields().Select(r.GetFieldDefinition).Single(f=>r.GetString(f.Name)==field);
 if(definition.DecodeSignature(new MetadataTypeNames(),(object?)null)!=expected)throw new Exception($"Reflected field type changed: {type}.{field}");passed++;
}
void Property(MetadataReader r,TypeDefinition type,string property,string expected){
 var definition=type.GetProperties().Select(r.GetPropertyDefinition).Single(p=>r.GetString(p.Name)==property);
 if(definition.DecodeSignature(new MetadataTypeNames(),(object?)null).ReturnType!=expected)throw new Exception($"Reflected property type changed: {property}");passed++;
}
Inspect(Path.Combine(game,"assembly_valheim.dll"),r=>{
 foreach(var m in new[]{"OnDeath","ApplyDamage","GetLevel","GetHealth","IsBoss"})Method(r,"Character",m);Method(r,"Character","ApplyDamage","hit");Field(r,"Character","m_lastHit");
 Method(r,"Player","OnDeath");Method(r,"Player","GetPlayerID");Method(r,"Inventory","GetBoundItems");Method(r,"Humanoid","GetCurrentWeapon");Method(r,"CharacterDrop","OnDeath");Method(r,"CharacterDrop","DropItems");Method(r,"Humanoid","Pickup","go");
 foreach(var m in new[]{"Awake","Start","OnDestroy","AutoStackItems","SaveToZDO","Load"})Method(r,"ItemDrop",m);
 Field(r,"Minimap","m_hasGenerated");Field(r,"Minimap","m_mapLargeShader");Field(r,"Minimap","m_mapTexture");Field(r,"Minimap","m_heightTexture");Field(r,"Minimap","m_forestMaskTexture");Method(r,"Character","GetDamageModifiers");Method(r,"ZNet","GetWorldName");Method(r,"MessageHud","ShowMessage");Field(r,"Minimap","m_explored");Field(r,"Minimap","m_textureSize");Field(r,"Minimap","m_pixelSize");Method(r,"WorldGenerator","GetBiome");Method(r,"WorldGenerator","GetHeight");
 foreach(var f in new[]{"m_playerID","m_playerName","m_publicRefPos","m_rpc"})Field(r,"ZNetPeer",f);
});
var epic=Path.Combine(plugins,"RandyKnapp-EpicLoot","EpicLoot.dll");if(File.Exists(epic))Inspect(epic,r=>{Method(r,"EpicLoot.ItemDataExtensions","GetMagicItem");Method(r,"EpicLoot.LootRoller","SpawnLootForDrop","initializeObject");Method(r,"EpicLoot.MagicItem","GetEffects","includeSocketed");Method(r,"EpicLoot.MagicItem","GetEffectText","effect","rarity","showRange","legendaryID");Field(r,"EpicLoot.MagicItem","Rarity");Method(r,"EpicLoot.MagicItemEffect","get_EffectType");Field(r,"EpicLoot.MagicItemEffect","EffectValue");});
var sls=Path.Combine(plugins,"MidnightMods-StarLevelSystem","StarLevelSystem.dll");if(File.Exists(sls))Inspect(sls,r=>{const string type="StarLevelSystem.modules.Loot.LootPerformanceChanges";Method(r,type,"DropItemsImmediate","dropThatCharacterDrop");Method(r,type,"DropItemsAsync","dropThatCharacterDrop");var iterator=Type(r,type).GetNestedTypes().Select(r.GetTypeDefinition).Single(t=>r.GetString(t.Name).Contains("DropItemsAsync"));if(!iterator.GetMethods().Select(r.GetMethodDefinition).Any(m=>r.GetString(m.Name)=="MoveNext")||!iterator.GetFields().Select(r.GetFieldDefinition).Any(f=>r.GetString(f.Name)=="dropThatCharacterDrop"))throw new Exception("SLS iterator layout changed");passed++;});
Inspect(Path.Combine(game,"assembly_valheim.dll"),r=>{Method(r,"ZNet","GetTimeSeconds");Method(r,"ZDO","GetLong");Method(r,"Inventory","GetAllItems");Method(r,"Character","GetMaxHealth");});
if(File.Exists(epic))Inspect(epic,r=>{
 Method(r,"EpicLoot.Adventure.Feature.BountiesAdventureFeature","OnBountyTargetSlain","saveData","bountyID","monsterID","isAdd");
 Method(r,"EpicLoot.Adventure.AdventureSaveData","GetBountyInfoByID","bountyID");
 foreach(var field in new[]{"PlayerID","State","Target","TargetName"})Field(r,"EpicLoot.Adventure.BountyInfo",field);
 Method(r,"EpicLoot.Adventure.BountyInfo","get_ID");Field(r,"EpicLoot.Adventure.BountyTargetInfo","MonsterID");
});
// Optional SLS integration resolves these members only when its assembly is present.
if(File.Exists(sls))Inspect(sls,r=>{
 const string config="StarLevelSystem.common.ValConfig";
 foreach(var name in new[]{"EnableNemesisSystem","EnableZoneScalingBonus","EnableZoneMapOverlay","ZoneOverlayAboveFog"})
  TypedField(r,config,name,"BepInEx.Configuration.ConfigEntry`1<Boolean>");
 TypedField(r,config,"ZoneOverlayColorTransparency","BepInEx.Configuration.ConfigEntry`1<Single>");
 const string zones="StarLevelSystem.Data.ZoneScaleSystemData";
 TypedField(r,zones,"Zones","System.Collections.Generic.List`1<ZoneData>");TypedField(r,zones,"zonesBuilt","Boolean");
 var zone=Type(r,"StarLevelSystem.common.DataObjects").GetNestedTypes().Select(r.GetTypeDefinition).Single(t=>r.GetString(t.Name)=="ZoneData");
 foreach(var name in new[]{"MinX","MaxX","MinZ","MaxZ"})Property(r,zone,name,"Single");
 Property(r,zone,"ZoneLevel","Int32");
 TypedField(r,"StarLevelSystem.modules.Colorization","zoneOverlayColors","System.Collections.Generic.List`1<UnityEngine.Color>");
});
Inspect(Path.Combine(game,"assembly_valheim.dll"),r=>{
 TypedField(r,"ZNetPeer","m_characterID","ZDOID");
 Signature(r,"ZDO","GetFloat","String","Single&");
});
// Inspect the built Sagas binary without loading it or any proprietary dependency.
var sagas=args.Length>2?args[2]:Path.GetFullPath("src/Sagas.Plugin/bin/Release/netstandard2.1/ValheimSagas.dll");
if(File.Exists(sagas))Inspect(sagas,r=>{
 var refs=r.AssemblyReferences.Select(r.GetAssemblyReference).Select(a=>r.GetString(a.Name)).ToArray();
 foreach(var optional in new[]{"EpicLoot","StarLevelSystem"}){
  if(refs.Contains(optional,StringComparer.OrdinalIgnoreCase))throw new Exception("Hard optional-mod assembly reference: "+optional);passed++;
 }
 var found=new HashSet<string>();
 foreach(var handle in Type(r,"ValheimSagas.SagasPlugin").GetCustomAttributes()){
  var a=r.GetCustomAttribute(handle);if(a.Constructor.Kind!=HandleKind.MemberReference)continue;
  var ctor=r.GetMemberReference((MemberReferenceHandle)a.Constructor);if(ctor.Parent.Kind!=HandleKind.TypeReference)continue;
  var type=r.GetTypeReference((TypeReferenceHandle)ctor.Parent);if(r.GetString(type.Name)!="BepInDependency")continue;
  var blob=r.GetBlobReader(a.Value);if(blob.ReadUInt16()!=1)throw new Exception("Invalid dependency attribute");
  var id=blob.ReadSerializedString();if(id is not ("randyknapp.mods.epicloot" or "MidnightsFX.StarLevelSystem"))continue;
  if(blob.ReadInt32()!=2)throw new Exception("Optional mod declared as hard BepInEx dependency: "+id);
  found.Add(id);passed++;
 }
 if(found.Count!=2)throw new Exception("Expected both optional mod declarations in Sagas plugin");passed++;
});
else Console.WriteLine("SKIP built plugin dependency audit: build Sagas.Plugin first or supply binary as third argument.");
// Read actual installed metadata without loading game assemblies or executing
// native calls. This proves API compatibility, not correct GPU output.
Inspect(Path.Combine(game,"UnityEngine.ParticleSystemModule.dll"),r=>{
 foreach(var method in new[]{"BakeMesh","BakeTrailsMesh"})Signature(r,"UnityEngine.ParticleSystemRenderer",method,"UnityEngine.Mesh","UnityEngine.Camera","UnityEngine.ParticleSystemBakeMeshOptions");
 foreach(var field in new[]{"Default","BakeRotationAndScale","BakePosition"})Field(r,"UnityEngine.ParticleSystemBakeMeshOptions",field);
 Method(r,"UnityEngine.ParticleSystemRenderer","get_trailMaterial");
});
Inspect(Path.Combine(game,"UnityEngine.CoreModule.dll"),r=>{
 Signature(r,"UnityEngine.TrailRenderer","BakeMesh","UnityEngine.Mesh","UnityEngine.Camera","Boolean");
 Signature(r,"UnityEngine.LineRenderer","BakeMesh","UnityEngine.Mesh","UnityEngine.Camera","Boolean");
 foreach(var property in new[]{"type","color","intensity","range","spotAngle","innerSpotAngle","cullingMask","shadows","colorTemperature","useColorTemperature","renderMode"}){
  Method(r,"UnityEngine.Light","get_"+property);Method(r,"UnityEngine.Light","set_"+property);
 }
 foreach(var property in new[]{"ambientProbe","ambientMode","ambientSkyColor","ambientEquatorColor","ambientGroundColor","ambientIntensity","ambientLight","reflectionIntensity","defaultReflectionMode","customReflectionTexture","fog","sun"}){
  Method(r,"UnityEngine.RenderSettings","get_"+property);Method(r,"UnityEngine.RenderSettings","set_"+property);
 }
 Signature(r,"UnityEngine.Rendering.SphericalHarmonicsL2","AddAmbientLight","UnityEngine.Color");
 Signature(r,"UnityEngine.Cubemap",".ctor","Int32","UnityEngine.TextureFormat","Boolean");
 Signature(r,"UnityEngine.Cubemap","SetPixels","UnityEngine.Color[]","UnityEngine.CubemapFace");
 Signature(r,"UnityEngine.Cubemap","Apply","Boolean","Boolean");
 foreach(var property in new[]{"lightProbeUsage","reflectionProbeUsage"})Method(r,"UnityEngine.Renderer","set_"+property);
 Signature(r,"UnityEngine.Transform","IsChildOf","UnityEngine.Transform");
 Signature(r,"UnityEngine.Shader","GetGlobalColor","Int32");Signature(r,"UnityEngine.Shader","SetGlobalColor","Int32","UnityEngine.Color");
 Signature(r,"UnityEngine.Shader","GetGlobalFloat","Int32");Signature(r,"UnityEngine.Shader","SetGlobalFloat","Int32","Single");
});
if(File.Exists(epic))Inspect(epic,r=>{
 Method(r,"EpicLoot.VisEquipment_Patch","AttachItem_Postfix","__instance","__result","itemHash");
 Method(r,"EpicLoot.VisEquipment_Patch","RefreshPlayerFx","player");
 Method(r,"EpicLoot.VisEquipment_Patch","GetEquipFxName","equippedItem","mode");
});
Console.WriteLine($"PASS {passed} installed-assembly metadata checks. No game code executed; this does not validate Harmony patch execution or gameplay.");

sealed class MetadataTypeNames:ISignatureTypeProvider<string,object?> {
 public string GetArrayType(string elementType,ArrayShape shape)=>elementType+"["+new string(',',shape.Rank-1)+"]";
 public string GetByReferenceType(string elementType)=>elementType+"&";
 public string GetFunctionPointerType(MethodSignature<string> signature)=>"function";
 public string GetGenericInstantiation(string genericType,ImmutableArray<string> typeArguments)=>genericType+"<"+string.Join(",",typeArguments)+">";
 public string GetGenericMethodParameter(object? context,int index)=>"!!"+index;
 public string GetGenericTypeParameter(object? context,int index)=>"!"+index;
 public string GetModifiedType(string modifier,string unmodifiedType,bool isRequired)=>unmodifiedType;
 public string GetPinnedType(string elementType)=>elementType;
 public string GetPointerType(string elementType)=>elementType+"*";
 public string GetPrimitiveType(PrimitiveTypeCode typeCode)=>typeCode.ToString();
 public string GetSZArrayType(string elementType)=>elementType+"[]";
 public string GetTypeFromDefinition(MetadataReader reader,TypeDefinitionHandle handle,byte rawTypeKind){var t=reader.GetTypeDefinition(handle);return Name(reader,t.Namespace,t.Name);}
 public string GetTypeFromReference(MetadataReader reader,TypeReferenceHandle handle,byte rawTypeKind){var t=reader.GetTypeReference(handle);return Name(reader,t.Namespace,t.Name);}
 public string GetTypeFromSpecification(MetadataReader reader,object? context,TypeSpecificationHandle handle,byte rawTypeKind)=>reader.GetTypeSpecification(handle).DecodeSignature(this,context);
 static string Name(MetadataReader reader,StringHandle ns,StringHandle name){var space=reader.GetString(ns);return (space.Length==0?"":space+".")+reader.GetString(name);}
}
