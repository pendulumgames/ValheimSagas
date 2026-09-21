namespace ValheimSagas;
// Installed vanilla values are asserted by metadata tests. Unknown sprite-backed
// persisted pins are conservatively personal; transient mod locations are game pins.
internal static class MapPinPolicy {
 internal static int Classify(int type,bool saved,bool sprite,bool copied,bool deleted){
  if(copied||deleted||type<0||type==7||type==10||type==12||type==13)return 0;
  if(type==0||type==1||type==2||type==3||type==6)return saved?2:0;
  if(type==4||type==5||type==9||type==11||type==14||type==15||type==16||type==17)return 1;
  if(type==8)return saved?2:1;
  return sprite?(saved?2:1):0;
 }
}
