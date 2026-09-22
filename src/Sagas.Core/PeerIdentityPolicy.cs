namespace ValheimSagas;
public static class PeerIdentityPolicy {
 public static long Resolve(bool ready,bool playerPrefab,long peerOwner,long objectOwner,long objectPlayer,long advertisedPlayer){
  return ready&&playerPrefab&&peerOwner!=0&&peerOwner==objectOwner&&objectPlayer!=0&&(advertisedPlayer==0||advertisedPlayer==objectPlayer)?objectPlayer:0;
 }
}
