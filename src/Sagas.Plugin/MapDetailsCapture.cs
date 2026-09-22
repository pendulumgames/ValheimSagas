using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
namespace ValheimSagas;
public sealed partial class SagasPlugin {
 ConfigEntry<bool> sharePins=null!;RuntimeArt? pinArt;
 readonly Dictionary<string,Packet> pendingPinPackets=new Dictionary<string,Packet>();
 readonly HashSet<string> pinMediaSent=new HashSet<string>();
 static readonly FieldInfo pinsField=AccessTools.Field(typeof(Minimap),"m_pins");
 static readonly FieldInfo pinBitsField=AccessTools.Field(typeof(Minimap),"m_explored");
 static readonly FieldInfo pinSizeField=AccessTools.Field(typeof(Minimap),"m_textureSize");
 static readonly FieldInfo pinPixelField=AccessTools.Field(typeof(Minimap),"m_pixelSize");
 double pinScanMaximum;int pinExportCount;
 List<MapPin>? scanningPins;int pinCursor,pinSourceCount;float nextPinScan,nextPinResync;string pinOwner="",pinSignature="";bool pinConsent;Task<(string Signature,Packet[] Packets)>? pinBuild;int pinEpoch,buildingPinEpoch;bool pinUploadTurn;
 void SetupMapDetails(){sharePins=BindSetting("Privacy","SharePlayerPins",false,"Share your own manually placed map pins with website viewers. Off by default. Requires ShareMap. Personal pins can reveal marked locations outside explored terrain. Pins copied from another Viking's cartography are excluded.");pinArt=new RuntimeArt(Warn);}
 void ResetMapDetails(){pinEpoch++;scanningPins=null;pinCursor=0;pinOwner="";pinSignature="";nextPinScan=0;nextPinResync=0;pendingPinPackets.Clear();pinMediaSent.Clear();pinArt?.Clear();}
 bool SendMapDetails(){pinUploadTurn=!pinUploadTurn;if(!pinUploadTurn)return false;foreach(var packet in pendingPinPackets.Values)if(Send(packet))return true;return false;}
 void PumpMapDetails(){
  if(!ZNet.instance||World!=activeWorld||World==""||!Player.m_localPlayer||!Minimap.instance)return;
  if(!shareMap.Value){if(pinOwner!="")ResetMapDetails();return;}
  var owner=lastLocalId;if(owner=="")return;
  if(pinOwner!=owner||pinConsent!=sharePins.Value){ResetMapDetails();pinOwner=owner;pinConsent=sharePins.Value;}
  if(!RuntimeTerrainShader.Pending&&!artworkCaptureBusy())pinArt?.PumpIcons(true);
  if(pinBuild!=null){if(!pinBuild.IsCompleted)return;var job=pinBuild;pinBuild=null;if(job.IsFaulted){Warn("map pin encoding",job.Exception!);nextPinScan=Time.unscaledTime+30;return;}if(buildingPinEpoch==pinEpoch){var built=job.Result;if(built.Signature!=pinSignature||Time.unscaledTime>=nextPinResync){foreach(var packet in built.Packets)pendingPinPackets[packet.AckId]=packet;pinSignature=built.Signature;nextPinResync=Time.unscaledTime+120;Logger.LogInfo("Sagas map pins snapshot queued: "+pinExportCount+" icons, "+built.Packets.Length+" paced parts; maximum scan slice "+pinScanMaximum.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" ms. Game locations require exploration; personal annotations require opt-in.");}}}
  if(scanningPins==null){if(Time.unscaledTime<nextPinScan||pendingPinPackets.Count>0)return;scanningPins=new List<MapPin>();pinScanMaximum=0;pinCursor=0;pinSourceCount=((List<Minimap.PinData>)pinsField.GetValue(Minimap.instance)).Count;}
  var map=Minimap.instance;var source=(List<Minimap.PinData>)pinsField.GetValue(map);
  if(source.Count!=pinSourceCount){scanningPins=null;nextPinScan=Time.unscaledTime+2;return;}
  var bits=(System.Collections.BitArray)pinBitsField.GetValue(map);int size=(int)pinSizeField.GetValue(map);float pixel=(float)pinPixelField.GetValue(map);
  var scanTimer=System.Diagnostics.Stopwatch.StartNew();
  // Bounded work even for heavily annotated worlds; never scan undiscovered world locations.
  for(int work=0;work<16&&pinCursor<source.Count&&scanningPins.Count<2048;work++,pinCursor++){
   var p=source[pinCursor];var category=MapPinPolicy.Classify((int)p.m_type,p.m_save,p.m_icon,p.m_ownerID!=0,p.m_shouldDelete);bool personal=category==2;
   if(category==0||personal&&!pinConsent)continue;
   int x=Utils.RoundToInt(p.m_pos.x/pixel+size/2),z=Utils.RoundToInt(p.m_pos.z/pixel+size/2);
   if(!personal&&(x<0||z<0||x>=size||z>=size||!bits[z*size+x]))continue;
   var image=pinArt?.TryMapIcon(p.m_icon);string icon="";
   if(image!=null){icon=image.Id;if(pinMediaSent.Count<128&&pinMediaSent.Add(icon)){var packet=new Packet{AckId="pin-art:"+icon,Media=new MediaUpload{World=World,PlayerId=owner,Id=icon,Kind="map-icon",Png=image.Png}};pendingPinPackets[packet.AckId]=packet;}}
   var name=Localize(p.m_name??"");if(name=="")name=p.m_icon?p.m_icon.name:p.m_type.ToString();name=new string(name.Where(c=>!char.IsControl(c)).Take(100).ToArray());
   scanningPins.Add(new MapPin{Name=name,Type=p.m_type.ToString(),IconId=icon,X=p.m_pos.x,Z=p.m_pos.z,Personal=personal,Checked=p.m_checked});
  }
  pinScanMaximum=Math.Max(pinScanMaximum,scanTimer.Elapsed.TotalMilliseconds);
  if(pinCursor<source.Count&&scanningPins.Count<2048)return;
  var captured=scanningPins;pinExportCount=captured.Count;scanningPins=null;nextPinScan=Time.unscaledTime+15;var world=World;buildingPinEpoch=pinEpoch;
  pinBuild=Task.Run(()=>{var signature=JsonConvert.SerializeObject(captured);int count=Math.Max(1,(captured.Count+31)/32);var revision=Guid.NewGuid().ToString("N");var packets=new Packet[count];for(int i=0;i<count;i++){var packet=new Packet{AckId="pins:"+revision+":"+i,Pins=new MapPins{World=world,PlayerId=owner,Revision=revision,Index=i,Count=count,Pins=captured.Skip(i*32).Take(32).ToList()}};packet.Wire=JsonConvert.SerializeObject(packet);packets[i]=packet;}return (signature,packets);});
 }
 bool artworkCaptureBusy()=>artwork?.CaptureBusy??false;
}
