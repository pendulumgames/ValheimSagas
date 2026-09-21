using System;
namespace ValheimSagas;
/// <summary>Credential-free HTTP(S) address shared on explicit overlay requests.</summary>
public static class WebsiteAddress {
 public static string Validate(string? value){
  if(value==null||value.Length>2048||!Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri)||(uri.Scheme!="http"&&uri.Scheme!="https")||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!=""||string.IsNullOrEmpty(uri.Host)||uri.Host=="0.0.0.0"||uri.Host=="::"||uri.Host=="[::]")return "";
  return uri.AbsoluteUri.Length<=2048?uri.AbsoluteUri:"";
 }
}
