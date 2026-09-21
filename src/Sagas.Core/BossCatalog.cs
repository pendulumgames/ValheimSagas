using System;
namespace ValheimSagas;
// Confirmed from installed FrozenKing phase prefabs and English localization.
public static class BossCatalog {
 public static readonly (string Key,string Name,int Order,string Biome)[] Bosses={
  ("Eikthyr","Eikthyr",1,"Meadows"),("gd_king","The Elder",2,"BlackForest"),("Bonemass","Bonemass",3,"Swamp"),("Dragon","Moder",4,"Mountain"),
  ("GoblinKing","Yagluth",5,"Plains"),("SeekerQueen","The Queen",6,"Mistlands"),("Fader","Fader",7,"Ashlands"),("FrozenKing_p3","Kall Fimbulbringer",8,"DeepNorth")
 };
 public static string Prefab(string name)=>name.EndsWith("(Clone)",StringComparison.Ordinal)?name.Substring(0,name.Length-7).Trim():name;
 public static bool IntermediatePhase(string name){var p=Prefab(name);return p.Equals("FrozenKing",StringComparison.OrdinalIgnoreCase)||p.Equals("FrozenKing_p2",StringComparison.OrdinalIgnoreCase);}
 public static bool FinalNorthPhase(string name)=>Prefab(name).Equals("FrozenKing_p3",StringComparison.OrdinalIgnoreCase);
}
