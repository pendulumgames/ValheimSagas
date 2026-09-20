using ValheimSagas;

static class TelemetryBudgetChecks {
 public static void Run(Action<bool,string> check){
  var budget=new TelemetryBudget();budget.Reset(0);
  check(!budget.TrySpend(TelemetryBudget.Artwork,90000,0,0),"Connecting client does not immediately burst artwork");
  check(!budget.TrySpend(TelemetryBudget.Map,22000,30,TelemetryBudget.QueueThreshold),"Existing game queue pauses telemetry");
  check(!budget.TrySpend(TelemetryBudget.Map,22000,30,-1),"Unavailable queue measurement fails closed");
  check(budget.TrySpend(TelemetryBudget.Map,22000,30,0),"Map resumes when gameplay queue drains");
  check(!budget.TrySpend(TelemetryBudget.Artwork,90000,30,0),"Different lanes cannot burst in the same frame");
  check(budget.TrySpend(TelemetryBudget.Artwork,90000,30.25,0),"Portrait has its own allowance despite map import");
  check(!budget.TrySpend(TelemetryBudget.Events,TelemetryBudget.MaximumPacketBytes+1,100,0),"Oversize UTF8 wire payload cannot bypass budget");
  budget.Reset(100);check(!budget.TrySpend(TelemetryBudget.Profile,1000,100,0),"World change discards accumulated allowance");

  // Synthetic saturated sender: snapshots, events, two queued map tiles and a
  // maximum-size portrait contend while gameplay occasionally owns the queue.
  budget.Reset(0);int[] sizes={1024,8192,22000,88000};long[] bytes=new long[4];int[] sent=new int[4];
  double last=-1,portraitFinished=0;
  for(int frame=0;frame<6000;frame++){
   double now=frame/10d;int queued=frame%100<20?32768:0;
   for(int n=0;n<4;n++){
    int lane=(frame+n)%4;if(lane==TelemetryBudget.Artwork&&sent[lane]>=16)continue;
    if(!budget.TrySpend(lane,sizes[lane],now,queued))continue;
    check(queued==0&&now-last>=.249,"Paced sender never adds to a blocked queue or bursts across lanes");
    last=now;bytes[lane]+=sizes[lane];sent[lane]++;
    if(lane==TelemetryBudget.Artwork&&sent[lane]==16)portraitFinished=now;
   }
  }
  int[] rates={8192,4096,3072,8192};
  for(int lane=0;lane<4;lane++)check(sent[lane]>0&&bytes[lane]<=rates[lane]*600L,"Each lane makes progress within its byte allowance");
  check(portraitFinished>0&&portraitFinished<MediaTransfer.ExpirySeconds,"Maximum portrait completes before expiry with concurrent traffic and queue pauses");
 }
}
