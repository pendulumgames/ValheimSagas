using System;
using System.Collections.Generic;
using System.Globalization;
namespace ValheimSagas;
public sealed class TimeWindow {
 public DateTime From {get;} public DateTime To {get;}
 public TimeWindow(DateTime from, DateTime to) { if(from>to) throw new ArgumentException("Start must precede end."); From=from;To=to; }
 public bool Contains(DateTime utc)=>utc>=From && utc<=To;
 public static TimeWindow Parse(string? range,string? from,string? to,DateTime now) {
  var minutes = new Dictionary<string,int>{{"30m",30},{"1h",60},{"6h",360},{"12h",720},{"1d",1440},{"3d",4320},{"7d",10080}};
  if(string.IsNullOrEmpty(range)||range=="all")return new TimeWindow(DateTime.MinValue,now);
  if(minutes.TryGetValue(range!,out int m))return new TimeWindow(now.AddMinutes(-m),now);
  if(range=="custom" && DateTimeOffset.TryParse(from,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var f) && DateTimeOffset.TryParse(to,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var t))return new TimeWindow(f.UtcDateTime,t.UtcDateTime);
  throw new ArgumentException("Invalid time filter. Use all, 30m, 1h, 6h, 12h, 1d, 3d, 7d or custom with ISO dates.");
 }
}
