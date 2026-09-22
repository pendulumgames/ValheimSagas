using System;
using System.Collections.Generic;
using System.Linq;
namespace ValheimSagas;

// Version 2 packets only: old servers ignore these rather than receiving an oversized legacy upload.
public sealed class MediaChunk {
 public string World {get;set;}="";
 public string Id {get;set;}="";
 public int TotalBytes {get;set;}
 public int Index {get;set;}
 public byte[] Data {get;set;}=Array.Empty<byte>();
}

// Called on the game thread. The authenticated owner is supplied by the RPC receiver, never the client payload.
public sealed class MediaTransfer {
 public const int ChunkBytes=64*1024, MaximumTransfers=16;
 public const double ExpirySeconds=1200;
 sealed class Transfer {
  public string World="",Id="";
  public byte[] Bytes=Array.Empty<byte>();
  public bool[] Received=Array.Empty<bool>();
  public double Started;
 }
 readonly Dictionary<string,Transfer> pending=new Dictionary<string,Transfer>();
 public int PendingCount=>pending.Count;
 public void Clear()=>pending.Clear();
 public void Expire(double now){foreach(var owner in pending.Where(x=>now-x.Value.Started>=ExpirySeconds).Select(x=>x.Key).ToArray())pending.Remove(owner);}
 public static int ChunkCount(int bytes)=>(bytes+ChunkBytes-1)/ChunkBytes;
 public static bool NeedsChunks(MediaUpload media)=>media.Kind=="portrait"&&(media.Png.Length>SagaService.MaximumIconBytes||media.Png.Length>=24&&(ReadDimension(media.Png,16)>512||ReadDimension(media.Png,20)>512));
 static long ReadDimension(byte[] bytes,int offset)=>((long)bytes[offset]<<24)|((long)bytes[offset+1]<<16)|((long)bytes[offset+2]<<8)|bytes[offset+3];
 public static MediaChunk Slice(MediaUpload media,int index){
  if(media.Kind!="portrait"||media.Png.Length<33||media.Png.Length>SagaService.MaximumPortraitBytes||index<0||index>=ChunkCount(media.Png.Length))throw new ArgumentOutOfRangeException(nameof(index));
  var data=new byte[Math.Min(ChunkBytes,media.Png.Length-index*ChunkBytes)];Array.Copy(media.Png,index*ChunkBytes,data,0,data.Length);
  return new MediaChunk{World=media.World,Id=media.Id,TotalBytes=media.Png.Length,Index=index,Data=data};
 }
 public MediaUpload? Accept(string owner,string world,MediaChunk chunk,double now){
  Expire(now);
  if(string.IsNullOrEmpty(owner)||owner.Length>100||string.IsNullOrEmpty(world)||world.Length>100||chunk==null||chunk.World!=world||chunk.Id==""||!SagaService.MediaId(chunk.Id)||chunk.TotalBytes<33||chunk.TotalBytes>SagaService.MaximumPortraitBytes||chunk.Index<0||chunk.Index>=ChunkCount(chunk.TotalBytes)||chunk.Data==null||chunk.Data.Length!=Math.Min(ChunkBytes,chunk.TotalBytes-chunk.Index*ChunkBytes))return null;
  if(!pending.TryGetValue(owner,out var transfer)||transfer.Id!=chunk.Id||transfer.World!=world){
   if(transfer==null&&pending.Count>=MaximumTransfers)return null;
   transfer=new Transfer{World=world,Id=chunk.Id,Bytes=new byte[chunk.TotalBytes],Received=new bool[ChunkCount(chunk.TotalBytes)],Started=now};pending[owner]=transfer;
  }
  if(transfer.Bytes.Length!=chunk.TotalBytes)return null;
  Array.Copy(chunk.Data,0,transfer.Bytes,chunk.Index*ChunkBytes,chunk.Data.Length);transfer.Received[chunk.Index]=true;
  if(transfer.Received.Any(x=>!x))return null;
  pending.Remove(owner);
  var media=new MediaUpload{World=world,PlayerId=owner,Id=transfer.Id,Kind="portrait",Png=transfer.Bytes};
  return SagaService.ValidMedia(media)?media:null;
 }
}
