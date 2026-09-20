using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValheimSagas;

int assertions = 0;
void Check(bool condition, string reason) { assertions++; if (!condition) throw new Exception(reason); }
var facts = new List<SagaEvent> {
 new() { Id="event-secret-1", World="world-secret", PlayerId="account-secret", PlayerName="Astrid", Name="Troll", Kind="kill", Stars=3, Utc=new DateTime(2026,9,1,0,0,0,DateTimeKind.Utc), X=13579, Z=24680 },
 new() { Id="event-secret-2", World="world-secret", PlayerId="account-secret", PlayerName="Astrid", Name="Silver sword", Kind="collect", Amount=2, Quality=4, Rarity="Epic", Utc=new DateTime(2026,9,2,0,0,0,DateTimeKind.Utc) }
};
var options = new SagaOptions();
var handler = new FakeHandler();
var client = new HttpClient(handler);
var engine = new LoreEngine(options, client, () => true);
var local = await engine.GenerateAsync("Astrid", facts, null, default);
Check(handler.Calls == 0 && local.Model == "local-template", "No external calls without opt in/key");
Check(local.Facts.Count == 2 && local.EventIds.Count == 2 && local.Facts[1].Contains("quality 4"), "Retain factual quality, rarity, support and dates");
Check(local.Text=="" && local.Summary.Contains("collected items 2"), "No template story without OpenRouter; factual summary retained");
Check(local.CharacterBio=="", "No fabricated local biography without credentials");
var career = facts.Concat(new[]{new SagaEvent { Id="career-secret-3",World="world-secret",PlayerId="account-secret",Kind="kill",Name="Troll",Boss=true,Utc=facts[0].Utc.AddDays(-1) },new SagaEvent { Id="foreign",World="another-world",PlayerId="account-secret",Kind="kill",Name="Must not appear" }}).ToArray();
var careerLocal = await engine.GenerateAsync("Astrid", facts, local, default, career);
Check(careerLocal.CharacterBio=="" && !string.Join(" ",careerLocal.Facts).Contains("Must not appear"), "No template biography and factual world isolation");
var reordered = await engine.GenerateAsync("Astrid", new[] {facts[1],facts[0],facts[0]}, null, default);
Check(reordered.Id == local.Id && reordered.EventIds.Count == 2, "Stable identity independent of order and repeats");
using(var legacyHash=System.Security.Cryptography.SHA256.Create()) {
 var legacyInput=JsonConvert.SerializeObject(new{world=local.World,player=local.PlayerId,events=facts.OrderBy(e=>e.Utc).ThenBy(e=>e.Id,StringComparer.Ordinal).Select(e=>e.Id).ToArray()});
 var legacyId="chapter-"+BitConverter.ToString(legacyHash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(legacyInput))).Replace("-","").ToLowerInvariant();
 Check(local.Id!=legacyId,"New generated chapter namespace cannot collide with legacy template ID");
}
options.LoreEnabled = true;
await engine.GenerateAsync("Astrid", facts, null, default);
Check(handler.Calls == 0, "Enabled without key remains offline");
options.OpenRouterKey = "test-key-secret";
options.LoreModel = "paid/model";
await engine.GenerateAsync("Astrid", facts, null, default);
Check(handler.Calls == 0, "Paid model rejected");
foreach (var model in new[] {"vendor/model:free:online", "vendor/model:nitro", " vendor/model:free", "vendor/model:free\n", "openrouter/auto"})
 Check(!LoreEngine.IsFreeModel(model), "Disallow paid/routing suffix " + model);
Check(LoreEngine.IsFreeModel("vendor/model-1.2:free"), "Allow pinned free variant");
options.LoreModel = "openrouter/free";
var denied = await new LoreEngine(options, client, () => false).GenerateAsync("Astrid", facts, null, default);
Check(handler.Calls == 0 && denied.Model == "local-template", "Budget must reserve before network");
handler.Responses.Enqueue(Valid());
var generated = await engine.GenerateAsync("Astrid", facts, local, default);
Check(handler.Calls == 1 && generated.Model == "vendor/actual:free" && generated.PromptVersion == LoreEngine.PromptVersion, "Record actual model and prompt version");
Check(generated.Summary == local.Summary && generated.Facts.SequenceEqual(local.Facts), "Model cannot mutate factual ledger or continuity capsule");
var body = JObject.Parse(handler.Bodies.Last());
Check((int?)body["provider"]?["max_price"]?["prompt"] == 0 && (int?)body["provider"]?["max_price"]?["completion"] == 0 && body["models"] == null, "Zero price limits, no paid fallback array");
Check((int?)body["provider"]?["preferred_max_latency"]?["p50"] == 2, "Routing preference shape");
Check(handler.Bodies.All(b => !new[]{"account-secret","world-secret","event-secret","test-key-secret","13579","24680"}.Any(b.Contains)), "No ids, credentials or coordinates in outbound data");
Check(handler.Auth == "Bearer test-key-secret", "Credential only in server authorization header");
Check(generated.CharacterBioModel == "local-template" && generated.CharacterBio == local.CharacterBio, "Valid chapter with omitted biography leaves missing biography absent");
handler.Responses.Enqueue(Valid("Beside the imagined fire, Astrid's recurring encounters become a refrain in a story still unfolding."));
var bioGenerated = await engine.GenerateAsync("Astrid", facts, careerLocal, default, career);
Check(bioGenerated.CharacterBio.StartsWith("Beside the imagined fire") && bioGenerated.CharacterBioModel == "vendor/actual:free", "Biography shares queued generation and stores separate provenance");
var bioData = JObject.Parse((string)JObject.Parse(handler.Bodies.Last())["messages"]![1]!["content"]!);
Check((int?)bioData["retainedCareer"]?["creditedKills"] == 2 && (string?)bioData["priorFictionalPortrayal"] == careerLocal.CharacterBio, "Career facts and fictional continuity passed distinctly");
Check(!handler.Bodies.Last().Contains("career-secret-3") && !handler.Bodies.Last().Contains("Must not appear"), "Career context excludes identifiers and other worlds");
handler.Responses.Enqueue(Valid("<script>This biography is invalid and must not replace the local portrayal.</script>"));
var invalidBio = await engine.GenerateAsync("Astrid", facts, local, default);
Check(invalidBio.Model == "vendor/actual:free" && invalidBio.CharacterBioModel == "local-template", "Invalid biography stays absent independently from valid chapter");
var bioDirectory = Path.Combine(Path.GetTempPath(), "sagas-bio-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(bioDirectory);
try {
 using(var db = new SagaStore(bioDirectory)) db.Chapter(bioGenerated);
 using(var db = new SagaStore(bioDirectory)) {
  var persisted=db.Chapters(bioGenerated.World).Single();
  Check(persisted.CharacterBio == bioGenerated.CharacterBio && persisted.CharacterBioModel == bioGenerated.CharacterBioModel && persisted.Facts.SequenceEqual(local.Facts), "Biography and immutable evidence survive database restart");
 }
} finally { Directory.Delete(bioDirectory,true); }
handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"not json\"}}]}") });
Check((await engine.GenerateAsync("Astrid", facts, local, default)).Model == "local-template", "Malformed output fallback");
handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 65537)) });
Check((await engine.GenerateAsync("Astrid", facts, local, default)).Model == "local-template", "Oversize body rejected");
handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized));
var calls = handler.Calls;
await engine.GenerateAsync("Astrid", facts, local, default);
Check(handler.Calls == calls + 1, "Authentication failure not retried");
handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
handler.Responses.Enqueue(Valid());
int reservations = 0;
Check((await new LoreEngine(options, client, () => {reservations++;return true;}).GenerateAsync("Astrid", facts, local, default)).Model != "local-template" && reservations == 2, "Rate limit retry consumes another reservation");
handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
reservations = 0; calls = handler.Calls;
await new LoreEngine(options, client, () => ++reservations == 1).GenerateAsync("Astrid", facts, local, default);
Check(handler.Calls == calls + 1, "Retries stop at request budget");
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
try { await engine.GenerateAsync("Astrid", facts, local, cancelled.Token); throw new Exception("Ignored cancellation"); } catch (OperationCanceledException) { assertions++; }
try { await engine.GenerateAsync("Astrid", new[]{facts[0],new SagaEvent {Id="other",World="different",PlayerId="account-secret"}}, null, default); throw new Exception("Mixed worlds"); } catch (ArgumentException) { assertions++; }
var serverFacts=new[]{facts[0],new SagaEvent{Id="server-secret-2",World="world-secret",PlayerId="second-account-secret",PlayerName="Bjorn",Name="Amber",Kind="collect",Amount=1,Utc=facts[1].Utc,X=13579,Z=24680}};
var serverOffline=new LoreEngine(new SagaOptions(),client,()=>true);
var serverLocal=await serverOffline.GenerateServerAsync("Northern Hall",serverFacts,null,default);
Check(serverLocal.Scope=="server"&&serverLocal.PlayerId==""&&serverLocal.Participants.Count==2,"Shared scope retains named individual participants");
Check(serverLocal.Text==""&&serverLocal.Facts[0].Contains("Astrid")&&serverLocal.Facts[1].Contains("Bjorn"),"No shared template prose; correct factual actors retained");
Check(serverLocal.ServerBio==""&&serverLocal.CharacterBio=="","No generated biography without a key");
Check(serverLocal.Id!=(await serverOffline.GenerateAsync("Astrid",new[]{facts[0]},null,default)).Id,"Shared and personal chapter identifiers do not collide");
Check(serverLocal.Id==(await serverOffline.GenerateServerAsync("Northern Hall",serverFacts.Reverse().ToArray(),null,default)).Id,"Shared id stable under reordered reports");
var later=new[]{new SagaEvent{Id="later-server-event",World="world-secret",PlayerId="second-account-secret",PlayerName="Bjorn",Name="Troll",Kind="kill",Utc=facts[1].Utc.AddDays(1)}};
var continued=await serverOffline.GenerateServerAsync("Northern Hall",later,serverLocal,default);
Check(continued.Participants.Any(p=>p.PlayerId=="account-secret")&&continued.Participants.Any(p=>p.PlayerId=="second-account-secret"),"Prior participants remain privacy dependencies without new events");
try{await serverOffline.GenerateServerAsync("Northern Hall",new[]{facts[0],new SagaEvent{Id="foreign-shared",World="other",PlayerId="p",PlayerName="Wrong world"}},null,default);throw new Exception("Mixed server worlds accepted");}catch(ArgumentException){assertions++;}
handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonConvert.SerializeObject(new{model="vendor/actual:free",choices=new[]{new{message=new{content=JsonConvert.SerializeObject(new{title="Two Voices",text="Astrid and Bjorn each have a place in this imagined hall; their recorded deeds keep their own names.",serverBio="The Northern Hall keeps the recorded tales of Astrid and Bjorn beside one imagined fire."})}}}}))});
reservations=0;
var sharedGenerated=await new LoreEngine(options,client,()=>{reservations++;return true;}).GenerateServerAsync("Northern Hall",serverFacts,serverLocal,default);
Check(reservations==1&&sharedGenerated.ServerBioModel=="vendor/actual:free"&&sharedGenerated.ServerBio.StartsWith("The Northern Hall"),"Server chapter and biography share one reserved free request");
var sharedRequest=handler.Bodies.Last();var sharedBody=JObject.Parse(sharedRequest);var sharedData=JObject.Parse((string)sharedBody["messages"]![1]!["content"]!);
Check((string?)sharedData["scope"]=="server"&&sharedData["participants"]!.Count()==2&&sharedRequest.Contains("Astrid")&&sharedRequest.Contains("Bjorn"),"External shared prompt preserves named actors and scope");
Check(!new[]{"world-secret","account-secret","second-account-secret","server-secret-2","13579","24680","test-key-secret"}.Any(sharedRequest.Contains),"Shared prompts exclude all participant ids, world, coordinates and credentials");
Check((int?)sharedBody["provider"]?["max_price"]?["prompt"]==0&&(int?)sharedBody["provider"]?["max_price"]?["completion"]==0&&sharedBody["models"]==null,"Shared request preserves strict zero-price routing");
calls=handler.Calls;var deniedServer=await new LoreEngine(options,client,()=>false).GenerateServerAsync("Northern Hall",serverFacts,null,default);
Check(calls==handler.Calls&&deniedServer.Model=="local-template","Exhausted shared request budget leaves story pending without network");
options.LoreAllowPaid=true;options.LoreMaxPrice=1.25m;options.LoreModel="vendor/paid";
handler.Responses.Enqueue(Valid());
await new LoreEngine(options,client,()=>true).GenerateAsync("Astrid",facts,null,default);
var paidRequest=JObject.Parse(handler.Bodies.Last());
Check((decimal?)paidRequest["provider"]?["max_price"]?["prompt"]==1.25m && (string?)paidRequest["model"]=="vendor/paid", "Explicit paid route carries price ceiling");
Check((string?)paidRequest["tool_choice"]=="none" && paidRequest["plugins"]!.Count()==0, "Narrative requests disable tool invocation and plugins");
options.LoreAllowPaid=false;options.LoreMaxPrice=0;options.LoreModel="@preset/my-saga";
handler.Responses.Enqueue(Valid());
await new LoreEngine(options,client,()=>true).GenerateAsync("Astrid",facts,null,default);
var presetRequest=JObject.Parse(handler.Bodies.Last());
Check((string?)presetRequest["model"]=="@preset/my-saga" && (decimal?)presetRequest["provider"]?["max_price"]?["completion"]==0, "Preset routing preserves explicit free ceiling");
// Host paid presets use the same safeguards for personal and shared sagas.
options.LoreAllowPaid=true;options.LoreMaxPrice=1m;options.LoreModel="@preset/fellowship";
handler.Responses.Enqueue(Valid());
await new LoreEngine(options,client,()=>true).GenerateServerAsync("Northern Hall",serverFacts,null,default);
var paidShared=JObject.Parse(handler.Bodies.Last());
Check((string?)paidShared["model"]=="@preset/fellowship"&&(decimal?)paidShared["provider"]?["max_price"]?["prompt"]==1m&&(decimal?)paidShared["provider"]?["max_price"]?["completion"]==1m,"Host paid preset reaches shared saga with input and output ceilings");
foreach(var invalid in new[]{0m,-1m,101m}){
 options.LoreMaxPrice=invalid;calls=handler.Calls;
 await new LoreEngine(options,client,()=>true).GenerateAsync("Astrid",facts,null,default);
 Check(calls==handler.Calls,"Invalid paid ceiling never makes a request");
}
options.LoreAllowPaid=false;options.LoreMaxPrice=1;options.LoreModel="vendor/paid";calls=handler.Calls;
await new LoreEngine(options,client,()=>true).GenerateAsync("Astrid",facts,null,default);
Check(handler.Calls==calls,"Positive ceiling alone never enables a paid direct model");
options.LoreModel="@preset/fellowship";handler.Responses.Enqueue(Valid());
await new LoreEngine(options,client,()=>true).GenerateAsync("Astrid",facts,null,default);
Check((decimal?)JObject.Parse(handler.Bodies.Last())["provider"]?["max_price"]?["prompt"]==0,"Paid opt-out forces zero cost even with a retained positive ceiling and preset");
options.LoreAllowPaid=true;options.LoreUseAccountPricing=true;options.LoreMaxPrice=0;
foreach(var route in new[]{"@preset/fellowship","vendor/paid"}){
 options.LoreModel=route;handler.Responses.Enqueue(Valid());
 await new LoreEngine(options,client,()=>true).GenerateServerAsync("Northern Hall",serverFacts,null,default);
 var delegated=JObject.Parse(handler.Bodies.Last());
 Check((string?)delegated["model"]==route&&delegated.Property("provider")==null,"Host paid routing leaves OpenRouter preset/account price and provider controls untouched");
 Check((int?)delegated["max_tokens"]==1000&&(string?)delegated["tool_choice"]=="none","Host paid routing preserves bounded narrative requests");
}
options.LoreAllowPaid=false;options.LoreModel="@preset/fellowship";handler.Responses.Enqueue(Valid());
await new LoreEngine(options,client,()=>true).GenerateServerAsync("Northern Hall",serverFacts,null,default);
Check((decimal?)JObject.Parse(handler.Bodies.Last())["provider"]?["max_price"]?["completion"]==0,"Account pricing flag cannot bypass paid opt-in");
await ContextChecks.Run(Check);
Console.WriteLine($"PASS: {assertions} lore checks (synthetic HTTP only; no credentials or game required).");

static HttpResponseMessage Valid(string? bio = null) => new(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(new {model="vendor/actual:free",choices=new[]{new {message=new{content=JsonConvert.SerializeObject(new {title="Runes in the Rain",text="The imagined rain whispered beside the hearth, while the ledger preserved the remembered deeds.",characterBio=bio})}}}})) };
sealed class FakeHandler : HttpMessageHandler {
 public int Calls; public string? Auth; public List<string> Bodies = new(); public Queue<HttpResponseMessage> Responses = new();
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
  Calls++; Auth=request.Headers.Authorization?.ToString(); Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
  return Responses.Count == 0 ? throw new Exception("Unexpected HTTP request") : Responses.Dequeue();
 }
}
