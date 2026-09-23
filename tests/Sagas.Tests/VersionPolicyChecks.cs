using ValheimSagas;
static class VersionPolicyChecks {
 public static void Run(Action<bool,string> check){
  check(VersionPolicy.Rejection("0.3.32","0.3.32",true)=="","Matching Sagas versions accepted");
  foreach(var other in new string?[]{null,"","0.3.31","0.3.33","garbage","<b>0.3.32</b>",new string('1',49)})check(VersionPolicy.Rejection("0.3.32",other,true)!="","Missing, malformed or mismatched clients rejected");
  foreach(var other in new string?[]{null,"","0.3.31","0.3.33"})check(VersionPolicy.Rejection("0.3.32",other,false)=="","Host can explicitly permit mixed or missing versions");
  check(VersionPolicy.Rejection("0.3.32","0.3.31",true).Contains("Server: 0.3.32; your client: 0.3.31"),"Mismatch names exact required and client versions");
 }
}
