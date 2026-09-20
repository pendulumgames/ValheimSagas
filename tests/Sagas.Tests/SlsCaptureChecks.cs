using System;
using System.Linq;
﻿using System.Collections;
using System.Diagnostics;
using ValheimSagas;
static class SlsCaptureChecks {
 sealed class Zone {public float MinX{get;set;}public float MaxX{get;set;}public float MinZ{get;set;}public float MaxZ{get;set;}public int ZoneLevel{get;set;}}
 public static void Run(Action<bool,string> check){
  var zones=Enumerable.Range(0,40000).Select(i=>new Zone{MinX=i%200*64-6400,MaxX=i%200*64-6336,MinZ=i/200*64-6400,MaxZ=i/200*64-6336,ZoneLevel=i%4+1}).ToList();
  var state=new SlsWorldState();int slices=0,previous=0;double max=0;var total=Stopwatch.StartNew();
  using(var capture=new SlsZoneCapture(state,zones,new[]{"#123456","#abcdef"})){bool done;do{var clock=Stopwatch.StartNew();done=capture.Step();max=Math.Max(max,clock.Elapsed.TotalMilliseconds);check(state.Zones.Count-previous<=128,"SLS frame has bounded zone work");previous=state.Zones.Count;slices++;}while(!done);}
  check(slices>=313&&state.Zones.Count==40000,"Large SLS world completes without omission");
  check(state.Zones[3].Color=="#abcdef"&&state.Zones[2].Color=="#123456","Configured SLS palette repeats by level");
  zones[0].ZoneLevel=9;check(state.Zones[0].Level==1,"Completed DTO is detached from live zone changes");
  var next=new SlsWorldState();using(var capture=new SlsZoneCapture(next,zones,new[]{"#654321"})){capture.Step();}check(next.Zones[0].Level==9&&next.Zones[0].Color=="#654321","Next capture observes changed levels and palette");
  using(var empty=new SlsZoneCapture(new(),null,null))check(empty.Step(),"Absent or disabled zones complete immediately");
  using(var changed=new SlsZoneCapture(new(),zones,null)){changed.Step();zones.RemoveAt(0);bool threw=false;try{changed.Step();}catch(InvalidOperationException){threw=true;}check(threw,"Rebuilt collection aborts partial snapshot instead of publishing it");}
  zones.Add(new());zones.Add(new());using(var oversized=new SlsZoneCapture(new(),zones,null)){bool threw=false;try{while(!oversized.Step()){};}catch(InvalidOperationException){threw=true;}check(threw,"Oversized zone collection remains bounded");}
  Console.WriteLine($"SLS capture synthetic 40000-zone check: {slices} slices, slowest {max:F2}ms, total check {total.Elapsed.TotalMilliseconds:F1}ms; not Unity frame timings.");
 }
}
