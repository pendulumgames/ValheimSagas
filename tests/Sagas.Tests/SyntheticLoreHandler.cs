using System.Net;
using Newtonsoft.Json;
internal sealed class SyntheticLoreHandler:HttpMessageHandler {
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonConvert.SerializeObject(new{model="fixture/generated",choices=new[]{new{message=new{content=JsonConvert.SerializeObject(new{title="Synthetic recorded chapter",text="Synthetic HTTP test narrative with a named supporting ledger; this is not recorded gameplay or a real model response.",characterBio="Synthetic character biography used only for deterministic automated checks.",serverBio="Synthetic fellowship biography used only for deterministic automated checks."})}}}}))});
}
