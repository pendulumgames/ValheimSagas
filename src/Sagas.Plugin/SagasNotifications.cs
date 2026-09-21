using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
namespace ValheimSagas;
/// <summary>Local Sagas messages only; never patches or suppresses another mod's HUD.</summary>
internal static class SagasNotifications {
 const int MaxSeen=4096;
 static readonly HashSet<string> Seen=new HashSet<string>(StringComparer.Ordinal);
 static readonly Queue<string> Order=new Queue<string>();
 static readonly Stopwatch Clock=Stopwatch.StartNew();
 static long nextAllowed;
 // Called on the Unity thread only, from initial Record, never from outbox replay/Send.
 internal static void Notify(SagaEvent e,string localPlayerId){
  if(e.Kind!="collect"||string.IsNullOrEmpty(localPlayerId)||e.PlayerId!=localPlayerId||
   string.IsNullOrEmpty(e.Id)||string.IsNullOrEmpty(e.Provenance)||(string.IsNullOrEmpty(e.Rarity)&&e.Sockets.Count==0)||e.Amount<=0)return;
  var id=e.World+"|"+e.Id;
  if(!Seen.Add(id))return;
  Order.Enqueue(id);while(Order.Count>MaxSeen)Seen.Remove(Order.Dequeue());
  // Rate-limited events remain seen; replay never causes a notification backlog.
  if(Clock.ElapsedMilliseconds<nextAllowed||!MessageHud.instance)return;
  if(e.Utc<DateTime.UtcNow.AddSeconds(-30)||e.Utc>DateTime.UtcNow.AddSeconds(2))return;
  nextAllowed=Clock.ElapsedMilliseconds+3000;
  var item=Plain(e.Name,100);if(item.Length==0)item="Rare treasure";
  var rarity=Plain(e.Rarity,30);if(e.Sockets.Count>0)rarity+=(rarity.Length>0?", ":"")+e.Sockets.Count+" sockets";
  var color=RarityColors.Normalize(e.RarityColor.Length>0?e.RarityColor:e.SocketColor);
  var label=item+(rarity.Length>0?" ("+rarity+")":"");
  if(color.Length>0)label="<color="+color+">"+label+"</color>";
  // Installed API inspected: TopLeft, text, amount=0, icon=null, hiddenHUD=false, log=true.
  // Do not duplicate into the game's message log; the website already retains the adventure.
  try { MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft,"Sagas · Found "+label+(e.Amount>1?" ×"+e.Amount:""),0,null,false,false); }
  catch { /* Optional notification failures must not interrupt event collection. */ }
 }
 static string Plain(string value,int max){
  var clean=Regex.Replace(value??"","<[^>]*>","");
  clean=new string(clean.Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').ToArray()).Trim();
  return clean.Length>max?clean.Substring(0,max):clean;
 }
}
