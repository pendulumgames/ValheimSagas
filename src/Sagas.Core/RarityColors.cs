using System;
namespace ValheimSagas;
/// <summary>Only opaque hex colors cross the telemetry boundary; never arbitrary CSS or Unity markup.</summary>
public static class RarityColors {
 public static bool Valid(string? value)=>value!=null&&(value.Length==0||(value.Length==7&&value[0]=='#'&&Hex(value.Substring(1))));
 static bool Hex(string value){foreach(var c in value)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F')))return false;return true;}
 public static string Normalize(string? value){
  if(string.IsNullOrEmpty(value)||value![0]!='#')return "";
  var hex=value.Substring(1);if(!Hex(hex))return "";
  if(hex.Length==3||hex.Length==4)return ("#"+hex[0]+hex[0]+hex[1]+hex[1]+hex[2]+hex[2]).ToUpperInvariant();
  if(hex.Length==6||hex.Length==8)return ("#"+hex.Substring(0,6)).ToUpperInvariant();
  return "";
 }
}
