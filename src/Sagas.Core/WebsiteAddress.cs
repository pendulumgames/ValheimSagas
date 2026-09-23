using System;
using System.Linq;
using System.Net;
namespace ValheimSagas;
/// <summary>Credential-free HTTP(S) address shared on explicit overlay requests.</summary>
public static class WebsiteAddress {
 public static string ListenPrefix(string? custom,int port,bool dedicated){
  if(port<1||port>65535)throw new ArgumentException("WebsitePort must be between 1 and 65535.");
  if(!string.IsNullOrWhiteSpace(custom)){
   string value=custom!.Trim(),test=value.Replace("http://*:","http://localhost:").Replace("http://+:","http://localhost:").Replace("https://*:","https://localhost:").Replace("https://+:","https://localhost:");
   if(!value.EndsWith("/",StringComparison.Ordinal)||!Uri.TryCreate(test,UriKind.Absolute,out var bind)||(bind.Scheme!="http"&&bind.Scheme!="https")||bind.UserInfo!=""||bind.Query!=""||bind.Fragment!=""||ListenPort(value)<1)throw new ArgumentException("ListenPrefix must be a complete HTTP(S) bind address ending in '/', without credentials, query or fragment. Leave it blank to use WebsitePort.");
   return value;
  }
  return "http://"+(dedicated?"*":"127.0.0.1")+":"+port+"/";
 }
 public static int ListenPort(string prefix){
  return Uri.TryCreate(prefix.Replace("http://*:","http://localhost:").Replace("http://+:","http://localhost:").Replace("https://*:","https://localhost:").Replace("https://+:","https://localhost:"),UriKind.Absolute,out var uri)?uri.Port:0;
 }
 // Only a direct-join address or explicitly configured host is eligible. Steam
 // IDs, PlayFab IDs, URL credentials and unspecified IPs are not browser hosts.
 public static string FromHost(string? address,int port){
  if(string.IsNullOrWhiteSpace(address)||address!.Length>253||port<1||port>65535)return "";
  string host=address.Trim();if(host.IndexOfAny(new[]{'/','\\','@','?','#'})>=0)return "";
  if(IPAddress.TryParse(host.Trim('[',']'),out var ip))host=ip.ToString();
  else if(Uri.TryCreate("http://"+host,UriKind.Absolute,out var parsed)&&parsed.AbsolutePath=="/")host=parsed.Host.Trim('[',']');else return "";
  if(host.All(char.IsDigit)||host.IndexOf('_')>=0)return "";
  if(!IPAddress.TryParse(host,out ip)&&Uri.CheckHostName(host)!=UriHostNameType.Dns)return "";
  if(ip!=null&&(ip.Equals(IPAddress.Any)||ip.Equals(IPAddress.IPv6Any)))return "";
  return Validate(new UriBuilder("http",host,port).Uri.AbsoluteUri);
 }
 public static string Validate(string? value){
  if(value==null||value.Length>2048||!Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri)||(uri.Scheme!="http"&&uri.Scheme!="https")||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!=""||string.IsNullOrEmpty(uri.Host)||uri.Host=="0.0.0.0"||uri.Host=="::"||uri.Host=="[::]")return "";
  return uri.AbsoluteUri.Length<=2048?uri.AbsoluteUri:"";
 }
}
