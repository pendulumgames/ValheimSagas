using System;
namespace ValheimSagas;
// Pure timing policy: debounce equipment bursts, bounded retry, infrequent safety refresh.
internal sealed class PortraitRefresh {
 string observed="",completed="";double changedAt,nextAttempt,nextFallback;
 internal void Reset(){observed=completed="";changedAt=nextAttempt=nextFallback=0;}
 internal bool Due(string signature,double now){if(signature!=observed){observed=signature;changedAt=now;}return now>=nextAttempt&&now-changedAt>=2&&(signature!=completed||now>=nextFallback);}
 internal void Started(double now){nextAttempt=now+10;}
 internal void Completed(string signature,double now){completed=signature;nextFallback=now+600;}
 internal void Failed(double now){nextAttempt=now+60;}
}
