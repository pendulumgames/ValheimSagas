using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using ValheimSagas;
internal static class DiagnosticsChecks {
 public static async Task Run(string root,Action<bool,string> check){
  var path=Path.Combine(root,"diagnostics");var messages=new List<string>();
  var options=new SagaOptions{DataDirectory=path,ViewerToken="viewer-token-secret-123456789",OpenRouterKey="openrouter-secret-test",Log=messages.Add};var diagnostics=new SagaDiagnostics(options);
  var exception=new InvalidOperationException("Test failure viewer-token-secret-123456789 and openrouter-secret-test",new IOException("Synthetic disk failure"));
  var id=diagnostics.Report("storage",exception);check(messages.Count==1&&messages[0].Contains(id)&&messages[0].Contains("Synthetic disk failure"),"Full diagnostic retains correlation ID and inner cause");
  check(!messages[0].Contains(options.ViewerToken)&&!messages[0].Contains(options.OpenRouterKey),"Diagnostic redacts configured secrets");
  var other=diagnostics.Report("http",new IOException("Second error"));check(diagnostics.Report("storage",exception)==id&&messages.Count==2,"Alternating repeated errors throttle independently");
  check(File.ReadAllText(Path.Combine(path,"diagnostics.log")).Contains(other),"Server diagnostics persisted");
  var probe=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
  options.ListenPrefix=$"http://127.0.0.1:{port}/";
  using(var service=new SagaService(options)){service.Start();service.Store.Dispose(); // Deliberate storage fault; never touch user data.
   using var client=new HttpClient{BaseAddress=new Uri(options.ListenPrefix)};client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",options.ViewerToken);
   var response=await client.GetAsync("api/state?world=test&range=all");var body=JObject.Parse(await response.Content.ReadAsStringAsync());
   check(response.StatusCode==HttpStatusCode.InternalServerError&&body["errorId"]!.ToString().Length==12,"HTTP storage fault supplies JSON correlation ID");
   check(body["error"]!.ToString().Contains("server diagnostics")&&!body.ToString().Contains(path),"Browser receives actionable error without exception paths");
   check(messages.Any(m=>m.Contains(body["errorId"]!.ToString())),"Browser error ID matches server exception log");
   check((await client.GetAsync("api/health")).IsSuccessStatusCode,"Authenticated health remains readable when database fails");
  }
 }
}
