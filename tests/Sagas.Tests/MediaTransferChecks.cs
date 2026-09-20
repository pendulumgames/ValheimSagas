using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json;
using ValheimSagas;

static class MediaTransferChecks {
 static string Hash(byte[] data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
 static byte[] Png(){
  // Real, clearly synthetic grayscale PNG: noisy upper rows exercise >48KiB storage and transport.
  const int width=1024,height=1536;var raw=new byte[(width+1)*height];var random=new Random(29);
  for(int y=0;y<512;y++)random.NextBytes(raw.AsSpan(y*(width+1)+1,width));
  using var output=new MemoryStream();output.Write(new byte[]{137,80,78,71,13,10,26,10});
  void U32(Stream stream,uint n){stream.WriteByte((byte)(n>>24));stream.WriteByte((byte)(n>>16));stream.WriteByte((byte)(n>>8));stream.WriteByte((byte)n);}
  void Chunk(string type,byte[] data){U32(output,(uint)data.Length);var tag=System.Text.Encoding.ASCII.GetBytes(type);output.Write(tag);output.Write(data);uint crc=0xffffffff;foreach(var b in tag.Concat(data)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}U32(output,crc^0xffffffff);}
  using var header=new MemoryStream();U32(header,width);U32(header,height);header.Write(new byte[]{8,0,0,0,0});Chunk("IHDR",header.ToArray());
  using var compressed=new MemoryStream();using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))z.Write(raw);Chunk("IDAT",compressed.ToArray());Chunk("IEND",Array.Empty<byte>());return output.ToArray();
 }
 public static async Task Run(string root,Action<bool,string> check){
  var png=Png();var media=new MediaUpload{World="high-resolution",PlayerId="viking",Kind="portrait",Png=png,Id=Hash(png)};
  check(png.Length>48*1024&&png.Length<=SagaService.MaximumPortraitBytes&&SagaService.ValidMedia(media),"Real 1024x1536 portrait above legacy byte limit accepted");
  check(MediaTransfer.NeedsChunks(media),"High resolution portrait uses bounded version 2 transport");
  media.Kind="icon";check(!SagaService.ValidMedia(media),"Portrait allowance does not enlarge icons");media.Kind="portrait";
  var corrupted=(byte[])png.Clone();corrupted[^20]^=1;media.Png=corrupted;check(!SagaService.ValidMedia(media),"Portrait content hash checked");media.Png=png;
  foreach(var offset in new[]{16,20}){var oversize=(byte[])png.Clone();oversize[offset+2]=0x10;media.Png=oversize;media.Id=Hash(oversize);check(!SagaService.ValidMedia(media),"Oversized portrait dimensions rejected");}
  media.Png=new byte[SagaService.MaximumPortraitBytes+1];media.Id=Hash(media.Png);check(!SagaService.ValidMedia(media),"Portrait upload above 1MiB rejected");media.Png=png;media.Id=Hash(png);
  var chunks=Enumerable.Range(0,MediaTransfer.ChunkCount(png.Length)).Select(i=>MediaTransfer.Slice(media,i)).ToArray();
  check(chunks.All(c=>c.Data.Length<=65536&&JsonConvert.SerializeObject(new{Version=2,MediaChunk=c}).Length<128000),"Every version 2 JSON packet remains below the legacy RPC bound");
  var receiver=new MediaTransfer();MediaUpload? result=null;
  foreach(var chunk in chunks.Reverse()){result=receiver.Accept("viking",media.World,chunk,1);if(chunk.Index>0)check(receiver.Accept("viking",media.World,chunk,2)==null,"Duplicate chunks do not complete early");}
  check(result!=null&&result.Png.SequenceEqual(png)&&result.PlayerId=="viking"&&receiver.PendingCount==0,"Out of order and duplicate chunks reassemble exact owner-bound bytes");
  receiver.Accept("viking",media.World,chunks[0],0);foreach(var c in chunks.Skip(1))receiver.Accept("other",media.World,c,1);
  check(receiver.PendingCount==2,"Different owners cannot complete one another's transfer");
  check(receiver.Accept("viking","different-world",chunks[1],1)==null,"Mismatched packet world rejected");
  receiver.Clear();check(receiver.PendingCount==0,"World exit clears partial uploads");
  receiver.Accept("viking",media.World,chunks[0],0);receiver.Expire(MediaTransfer.ExpirySeconds);check(receiver.PendingCount==0,"Incomplete uploads expire");
  receiver.Accept("viking",media.World,chunks[0],0);var mismatch=MediaTransfer.Slice(media,1);mismatch.TotalBytes--;check(receiver.Accept("viking",media.World,mismatch,1)==null,"Conflicting length rejected without overwriting assembly");
  var bad=MediaTransfer.Slice(media,1);bad.Data[0]^=1;receiver.Accept("viking",media.World,bad,1);foreach(var c in chunks.Skip(2))result=receiver.Accept("viking",media.World,c,2);
  check(result==null&&receiver.PendingCount==0,"Corrupted complete upload is discarded before persistence");
  for(int i=0;i<MediaTransfer.MaximumTransfers+3;i++)receiver.Accept("owner"+i,media.World,chunks[0],0);
  check(receiver.PendingCount==MediaTransfer.MaximumTransfers,"Global incomplete transfer memory is bounded to 16MiB");receiver.Expire(MediaTransfer.ExpirySeconds);
  foreach(var c in chunks)result=receiver.Accept("viking",media.World,c,MediaTransfer.ExpirySeconds+1);check(result!=null,"Retry after expiry or restart can reconstruct complete image");
  var probe=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
  var options=new SagaOptions{DataDirectory=Path.Combine(root,"large-media"),World=media.World,ListenPrefix=$"http://127.0.0.1:{port}/",RequireViewerToken=false};
  var player=new PlayerSnapshot{World=media.World,PlayerId=media.PlayerId,Name="Synthetic high resolution portrait",ShareProfile=true,PortraitId=media.Id,Online=false};
  using(var service=new SagaService(options)){
   service.Start();var oldPng=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=");var oldId=Hash(oldPng);
   service.UploadMedia(new(){World=media.World,PlayerId=media.PlayerId,Kind="portrait",Png=oldPng,Id=oldId});player.PortraitId=oldId;service.UpdatePlayer(player);service.Flush();
   player.PortraitId=media.Id;player.Gear.Add(new(){Name="Synthetic pending icon",IconId=new string('e',64)});service.UpdatePlayer(player);service.Flush();
   var pending=service.Store.Players(media.World).Single();check(pending.PortraitId==oldId&&pending.Gear[0].IconId==new string('e',64),"Previous portrait remains while a larger replacement uploads; icon references unchanged");
   check(service.UploadMedia(media)&&service.UpdatePlayer(player)&&service.Flush(),"Large portrait persisted alongside offline profile");
   check(service.Store.Players(media.World).Single().PortraitId==media.Id,"Next snapshot adopts the committed replacement portrait");
   player.PortraitId="";service.UpdatePlayer(player);service.Flush();check(service.Store.Players(media.World).Single().PortraitId=="","Explicit portrait clearing is preserved");
   player.PortraitId=media.Id;service.UpdatePlayer(player);service.Flush();
  }
  using(var service=new SagaService(options)){
   service.Start();using var client=new HttpClient{BaseAddress=new Uri(options.ListenPrefix)};var url=$"api/media/{media.Id}?player={media.PlayerId}&world={media.World}";
   using var response=await client.GetAsync(url);check(response.IsSuccessStatusCode&&response.Content.Headers.ContentType?.MediaType=="image/png"&&(await response.Content.ReadAsByteArrayAsync()).SequenceEqual(png),"Full portrait survives database restart and HTTP delivery without downscaling");
   player.ShareProfile=false;service.UpdatePlayer(player);service.Flush();check((await client.GetAsync(url)).StatusCode==HttpStatusCode.NotFound,"High resolution artwork retains profile privacy enforcement");
  }
 }
}
