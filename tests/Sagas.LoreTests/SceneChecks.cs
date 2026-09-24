using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValheimSagas;
static class SceneChecks {
 public static async Task Run(Action<bool,string> check){
  var events=Enumerable.Range(1,5).Select(i=>new SagaEvent{Id="private-event-"+i,World="private-world",PlayerId="private-player",Kind="kill",Name="Troll",Utc=DateTime.UtcNow.AddMinutes(i),X=12345,Z=23456}).ToArray();
  var handler=new FakeHandler();var client=new HttpClient(handler);var engine=new LoreEngine(new(){LoreEnabled=true,OpenRouterKey="private-key"},client,()=>true);
  object Scene(string title,params string[] refs)=>new{title,text="Beneath the dark boughs the tale found another turning, and the silence deepened.",eventRefs=refs};
  HttpResponseMessage Response(object scenes)=>new(HttpStatusCode.OK){Content=new StringContent(JsonConvert.SerializeObject(new{model="fixture:free",choices=new[]{new{message=new{content=JsonConvert.SerializeObject(new{title="Beneath the boughs",scenes})}}}}))};
  handler.Responses.Enqueue(Response(new[]{Scene("The approach","e1","e2"),Scene("Thunder","e3"),Scene("The road beyond","e4","e5")}));
  var c=await engine.GenerateAsync("Astrid",events,null,default);
  check(handler.Calls==1&&c.Scenes.Count==3,"Scenes and chapter use one generation request");
  check(c.Text==string.Join("\n\n",c.Scenes.Select(s=>s.Text)),"Full chapter is composed from the actual scene passages");
  check(c.Scenes[0].EventIds.SequenceEqual(new[]{events[0].Id,events[1].Id}),"Local reference aliases resolve to server-owned evidence IDs");
  check(!handler.Bodies[0].Contains("private-event")&&!handler.Bodies[0].Contains("12345")&&!handler.Bodies[0].Contains("private-key"),"Scene request omits real identifiers, coordinates and credentials");
  var path=Path.Combine(Path.GetTempPath(),"sagas-scenes-"+Guid.NewGuid().ToString("N"));
  using(var store=new SagaStore(path))store.Chapter(c);
  using(var store=new SagaStore(path))check(store.Chapters("private-world").Single().Scenes.Count==3,"Scene passages and evidence persist through restart");
  Directory.Delete(path,true);
  foreach(var scenes in new object[]{new[]{Scene("Unknown","e99")},new[]{Scene("Repeated","e1","e1")},new[]{Scene("Late","e3"),Scene("Early","e1")},new[]{Scene("Raw ID",events[0].Id)},Enumerable.Range(0,7).Select(i=>Scene("Too many","e1")).ToArray(),new[]{new{title="Markup",text="<script>bad and very long narrative output that must not be accepted</script>",eventRefs=new[]{"e1"}}}}){
   handler.Responses.Enqueue(Response(scenes));var before=handler.Calls;var invalid=await engine.GenerateAsync("Astrid",events,null,default);check(invalid.Scenes.Count==0&&invalid.Model=="local-template"&&handler.Calls==before+1,"Invalid scene output is rejected without extra generation calls");
  }
 }
}
