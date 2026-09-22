using ValheimSagas;
static class NetworkReliabilityChecks {
 public static void Run(Action<bool,string> check){
  check(PeerIdentityPolicy.Resolve(true,true,12,12,99,0)==99,"Owned character resolves without optional PlayerID RPC");
  check(PeerIdentityPolicy.Resolve(true,true,12,13,99,0)==0,"Other peer character rejected");
  check(PeerIdentityPolicy.Resolve(true,false,12,12,99,0)==0,"Non-player object rejected");
  check(PeerIdentityPolicy.Resolve(true,true,12,12,99,100)==0,"Conflicting identity rejected");
  check(PeerIdentityPolicy.Resolve(false,true,12,12,99,99)==0,"Unready connection rejected");
  check(TelemetryBudget.SteamHasHeadroom(16797,16000,0),"Ordinary ACK backlog allows small paced fragments");
  check(!TelemetryBudget.SteamHasHeadroom(16797,0,0),"Unsent backlog remains blocked");
  check(!TelemetryBudget.SteamHasHeadroom(30000,30000,0),"Next fragment must fit total ceiling");
  check(!TelemetryBudget.SteamHasHeadroom(32768,32768,0),"Total backlog ceiling retained");
  check(!TelemetryBudget.SteamHasHeadroom(1000,0,20000),"Queue delay blocks telemetry");
  check(!TelemetryBudget.SteamHasHeadroom(1000,2000,0),"Inconsistent metrics rejected");
  // Saturated four-lane sender, 20% network-pressure pauses, actual encoded fragment cost.
  var budget=new TelemetryBudget();budget.Reset(0);int turn=0,portraitBytes=0;int[] sent=new int[4];double completed=0;
  for(int frame=0;frame<12000;frame++){
   double now=frame/10d;int queue=frame%100<20?32768:0;
   for(int n=0;n<4;n++){int lane=(turn+n)%4;if(lane==3&&portraitBytes>=1399000)continue;
    if(!budget.Ready(lane,4096,now,queue)||!budget.TrySpend(lane,3450,now,queue))continue;
    sent[lane]++;turn=(lane+1)%4;if(lane==3){portraitBytes+=2400;if(portraitBytes>=1399000)completed=now;}break;
   }
  }
  check(sent.All(x=>x>0),"All fragment lanes make progress under concurrent load");
  check(completed>0&&completed<MediaTransfer.ExpirySeconds,"Maximum portrait can complete during saturated traffic and pressure pauses");
  var transfer=new WireTransfer();var bytes=Enumerable.Range(0,128000).Select(x=>(byte)x).ToArray();var id=Guid.NewGuid().ToString("N");int count=(bytes.Length+WireTransfer.Chunk-1)/WireTransfer.Chunk;
  byte[]? result=null;
  for(int i=0;i<count;i++) {var part=new WirePart{Id=id,Index=i,Count=count,Data=bytes.Skip(i*WireTransfer.Chunk).Take(WireTransfer.Chunk).ToArray()};check(System.Text.Encoding.UTF8.GetByteCount(Newtonsoft.Json.JsonConvert.SerializeObject(part))+128<4096,"Every encoded fragment fits small wire budget");result=transfer.Accept("peer",part,i*.3);if(i<count-1)check(result==null,"Partial packets never applied");}
  check(result!=null&&result.SequenceEqual(bytes),"Maximum packet reassembles exactly");
  check(transfer.Accept("bad",new WirePart{Id=id,Index=0,Count=100000,Data=new byte[1]},0)==null,"Unbounded fragment count rejected");
  transfer.Clear();var first=new WirePart{Id=id,Index=0,Count=2,Data=new byte[2400]};var last=new WirePart{Id=id,Index=1,Count=2,Data=new byte[1]};
  transfer.Accept("a",first,0);check(transfer.Accept("b",last,1)==null,"Peers cannot complete each other's fragments");check(transfer.Accept("a",last,181)==null,"Expired fragments discarded");
 }
}
