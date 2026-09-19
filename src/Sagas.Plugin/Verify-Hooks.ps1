param(
 [string]$GameManaged='C:/Program Files (x86)/Steam/steamapps/common/Valheim/valheim_Data/Managed',
 [string]$ProfilePath='C:/Users/mecra/AppData/Roaming/Thunderstore Mod Manager/DataFolder/Valheim/profiles/Deep North'
)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom((Join-Path $ProfilePath 'BepInEx/core/Mono.Cecil.dll')) | Out-Null
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameManaged 'assembly_valheim.dll'))
$checks=@(@('Character','OnDeath'),@('Player','OnDeath'),@('Character','ApplyDamage'),@('CharacterDrop','OnDeath'),@('CharacterDrop','DropItems'),@('ItemDrop','Awake'),@('ItemDrop','Start'),@('ItemDrop','OnDestroy'),@('ItemDrop','AutoStackItems'),@('Humanoid','Pickup'))
$count=0
foreach($check in $checks){
 $type=$game.MainModule.Types | Where-Object FullName -EQ $check[0]
 $methods=@($type.Methods | Where-Object Name -EQ $check[1])
 if($methods.Count -ne 1){throw ('Ambiguous/missing hook '+($check -join '.'))}
 Write-Output ('PASS '+$methods[0].FullName);$count++
}
$humanoid=$game.MainModule.Types|Where-Object Name -EQ Humanoid
foreach($name in @('get_RightItem','get_LeftItem','GetAmmoItem')){
 $methods=@($humanoid.Methods|Where-Object Name -EQ $name)
 if($methods.Count -ne 1 -or !$methods[0].IsPublic -or $methods[0].ReturnType.FullName -ne 'ItemDrop/ItemData'){throw "Held-item state API changed: $name"}
 $count++
}
$map=$game.MainModule.Types|Where-Object Name -EQ Minimap
$field=$map.Fields|Where-Object Name -EQ m_explored
if($field.FieldType.FullName -ne 'System.Collections.BitArray'){throw 'Minimap exploration field ABI changed'}
$count++
$slsPath=Join-Path $ProfilePath 'BepInEx/plugins/MidnightMods-StarLevelSystem/StarLevelSystem.dll'
if(Test-Path -LiteralPath $slsPath){
 $sls=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($slsPath)
 $type=$sls.MainModule.Types|Where-Object FullName -EQ 'StarLevelSystem.modules.Loot.LootPerformanceChanges'
 foreach($name in 'DropItemsImmediate','DropItemsAsync'){
  $method=@($type.Methods|Where-Object Name -EQ $name)
  if($method.Count -ne 1){throw "SLS method changed: $name"}
  Write-Output ('PASS '+$method[0].FullName);$count++
 }
 $iterator=$type.NestedTypes|Where-Object Name -Like '*DropItemsAsync*'
 if(@($iterator.Methods|Where-Object Name -EQ MoveNext).Count -ne 1){throw 'SLS async MoveNext changed'}
 if(($iterator.Fields|Where-Object Name -EQ dropThatCharacterDrop).FieldType.FullName -ne 'System.Boolean'){throw 'SLS async source field changed'}
 $count+=2
}
$epicPath=Join-Path $ProfilePath 'BepInEx/plugins/RandyKnapp-EpicLoot/EpicLoot.dll'
if(Test-Path -LiteralPath $epicPath){
 $epic=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($epicPath)
 $roller=$epic.MainModule.Types|Where-Object FullName -EQ 'EpicLoot.LootRoller'
 $spawn=@($roller.Methods|Where-Object Name -EQ SpawnLootForDrop)
 if($spawn.Count -ne 1 -or -not ($spawn[0].Parameters.Name -contains 'initializeObject')){throw 'Epic Loot physical spawn ABI changed'}
 $extensions=$epic.MainModule.Types|Where-Object FullName -EQ 'EpicLoot.ItemDataExtensions'
 if(@($extensions.Methods|Where-Object Name -EQ GetMagicItem).Count -ne 1){throw 'Epic Loot metadata API changed'}
 $paletteType=$epic.MainModule.Types|Where-Object FullName -EQ 'EpicLoot.EpicLoot'
 $palette=@($paletteType.Methods|Where-Object Name -EQ GetRarityColor)
 if($palette.Count -ne 1 -or $palette[0].ReturnType.FullName -ne 'System.String' -or $palette[0].Parameters[0].ParameterType.FullName -ne 'EpicLoot.ItemRarity'){throw 'Epic Loot configured rarity palette API changed'}
 $count+=3
}
Write-Output "$count metadata checks passed; this does not execute Harmony patches or the game runtime."
