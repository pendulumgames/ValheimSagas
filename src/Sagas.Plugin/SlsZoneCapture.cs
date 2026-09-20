using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
namespace ValheimSagas;
// Main-thread incremental copy. No game object survives in the completed DTO.
 internal sealed class SlsZoneCapture : IDisposable {
  internal readonly SlsWorldState State;
  readonly IEnumerator? zones;readonly string[] colors;
  readonly Dictionary<Type,PropertyInfo[]> properties=new Dictionary<Type,PropertyInfo[]>();
  internal SlsZoneCapture(SlsWorldState state,IEnumerable? source,string[]? palette){State=state;zones=source?.GetEnumerator();colors=palette??Array.Empty<string>();}
  internal bool Step(){
   if(zones==null)return true;var clock=Stopwatch.StartNew();int count=0;
   while(count++<128&&clock.Elapsed.TotalMilliseconds<2){
    if(!zones.MoveNext())return true;
    if(State.Zones.Count>=40000)throw new InvalidOperationException("SLS zone count exceeds the supported 40000-zone snapshot bound.");
    var zone=zones.Current;var type=zone.GetType();if(!properties.TryGetValue(type,out var props)){
     var names=new[]{"MinX","MaxX","MinZ","MaxZ","ZoneLevel"};props=new PropertyInfo[names.Length];for(int i=0;i<names.Length;i++)props[i]=type.GetProperty(names[i])??throw new InvalidOperationException("SLS zone property unavailable: "+names[i]);properties[type]=props;
    }
    var level=Convert.ToInt32(props[4].GetValue(zone));var color="#808080";
    if(level>=1&&colors.Length>0)color=colors[(level-1)%colors.Length];
    State.Zones.Add(new SlsZone{MinX=Convert.ToSingle(props[0].GetValue(zone)),MaxX=Convert.ToSingle(props[1].GetValue(zone)),MinZ=Convert.ToSingle(props[2].GetValue(zone)),MaxZ=Convert.ToSingle(props[3].GetValue(zone)),Level=level,Color=color});
   }return false;
  }
  public void Dispose(){(zones as IDisposable)?.Dispose();}
 }
