using System.Collections;
using ValheimSagas;
static class PluginScanChecks {
 internal static void Run(Action<bool,string> check) {
  check(BossTiming.ObserveDamage(0,100,100,120000)==120000,"Boss clock starts at first damage against full health");
  check(BossTiming.ObserveDamage(0,50,100,120000)==-1 && BossTiming.ObserveDamage(-1,20,100,121000)==-1,"Partially observed fight cannot acquire a misleading duration");
  check(BossTiming.ObserveDamage(120000,50,100,125000)==120000 && BossTiming.Elapsed(120000,125500)==5.5,"Shared persisted boss timer survives a later owner/hit without restart");
  check(BossTiming.ObserveDamage(120000,100,100,150000)==150000 && BossTiming.ObserveDamage(-1,100,100,150000)==150000,"Full healing starts a new complete attempt");
  check(BossTiming.Elapsed(0,1000)==null && BossTiming.Elapsed(-1,1000)==null && BossTiming.Elapsed(120000,110000)==null && BossTiming.Elapsed(120000,120000)==null,"Unknown and reversed world-clock durations are unavailable");
  check(BossTiming.Elapsed(1,604800001)==604800 && BossTiming.Elapsed(1,604801001)==null,"Excessively old boss attempts are excluded");
  var near=ExplorationScan.Nearby(-1,-65).ToArray();
  check(near[0]==(-1,-2),"Map priority starts at local cell, including negative coordinates");
  check(near.Length==81&&near.Distinct().Count()==81,"Local map priority is bounded with no duplicate cells");
  check(ExplorationScan.Nearby(10239,10239).All(c=>c.X<160&&c.Z<160),"Local map priority stays within supported world grid");
  var bits=new BitArray(64*64);for(int z=32;z<=40;z++)for(int x=32;x<=40;x++)bits[z*64+x]=true;
  check(ExplorationScan.FullyKnown(bits,64,8,0,0),"Completely personally explored cell is exportable");
  bits[40*64+40]=false;
  check(!ExplorationScan.FullyKnown(bits,64,8,0,0),"One unexplored edge pixel keeps complete cell behind fog");
  check(!ExplorationScan.FullyKnown(bits,64,8,1,1),"Nearby unexplored terrain cannot leak through priority scan");
  check(!ExplorationScan.FullyKnown(bits,64,8,4,0)&&!ExplorationScan.FullyKnown(new BitArray(4),64,8,0,0),"Out-of-range and incomplete minimap buffers remain private");
 }
}
