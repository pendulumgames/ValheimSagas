using System;
namespace ValheimSagas;
public static class VersionPolicy {
 public static bool Valid(string? value){if(string.IsNullOrEmpty(value)||value!.Length>48)return false;foreach(char c in value)if(c!='.'&&(c<'0'||c>'9'))return false;return Version.TryParse(value,out _);}
 public static string Rejection(string current,string? remote,bool required){if(!required)return "";if(!Valid(remote))return "This server requires Valheim Sagas "+current+". Install or enable that version, then reconnect.";if(!string.Equals(current,remote,StringComparison.Ordinal))return "Valheim Sagas version mismatch. Server: "+current+"; your client: "+remote+". Install the same version as the server, then reconnect.";return "";}
}
