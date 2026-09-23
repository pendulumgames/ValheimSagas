using ValheimSagas;
static class WebsiteSetupChecks {
 public static void Run(Action<bool,string> check){
  check(WebsiteAddress.ListenPrefix("",19908,true)=="http://*:19908/","Dedicated simple setup binds allocated port on all interfaces");
  check(WebsiteAddress.ListenPrefix("",8877,false)=="http://127.0.0.1:8877/","Local hosting retains loopback default");
  check(WebsiteAddress.ListenPrefix("http://127.0.0.1:9000/",19908,true)=="http://127.0.0.1:9000/","Existing custom binding is preserved");
  check(WebsiteAddress.ListenPrefix("http://0.0.0.0:9000/",19908,true)=="http://0.0.0.0:9000/","Bind addresses need not be browsable addresses");
  check(WebsiteAddress.ListenPort("http://*:19908/")==19908,"Wildcard port can be advertised to direct clients");
  foreach(var invalid in new[]{"http://*:19908","http://user:secret@example.com/","ftp://example.com/","http://example.com/?token=secret/"}){bool rejected=false;try{WebsiteAddress.ListenPrefix(invalid,8877,true);}catch(ArgumentException){rejected=true;}check(rejected,"Invalid listener override gets an actionable validation error");}
  check(WebsiteAddress.FromHost("74.112.78.28",19908)=="http://74.112.78.28:19908/","Direct server IP plus web port");
  check(WebsiteAddress.FromHost("74.112.78.28:2456",19908)=="http://74.112.78.28:19908/","Game port is replaced with web port");
  check(WebsiteAddress.FromHost("valheim.example.com:2456",19908)=="http://valheim.example.com:19908/","Configured DNS host supported");
  check(WebsiteAddress.FromHost("[2001:db8::1]:2456",19908)=="http://[2001:db8::1]:19908/","IPv6 bracketed with web port");
  foreach(var bad in new[]{"", "76561198012345678", "playfab/ABCDEF", "Steam_76561198012345678", "user:password@example.com", "example.com/?token=secret", "0.0.0.0", "::", "example.com/#token"})check(WebsiteAddress.FromHost(bad,19908)=="","Identity/credential/unspecified address is not an inferred browser endpoint: "+bad);
  foreach(var port in new[]{-1,0,65536}){bool rejected=false;try{WebsiteAddress.ListenPrefix("",port,true);}catch(ArgumentException){rejected=true;}check(rejected&&WebsiteAddress.FromHost("example.com",port)=="","Invalid web port rejected");}
  check(WebsiteAddress.Validate("https://sagas.example.com/")=="https://sagas.example.com/","Custom HTTPS browser URL preserved");
 }
}
