using System;
using System.IO;
using System.IO.Compression;
using System.Text;
namespace ValheimSagas;
public sealed class WirePayload { public byte[] Bytes=Array.Empty<byte>(); public bool Compressed; }
public static class WireCompression {
 public static WirePayload Encode(string json){
  var bytes=Encoding.UTF8.GetBytes(json);if(bytes.Length>WireTransfer.Maximum)throw new InvalidDataException("Upload too large");
  using var output=new MemoryStream();using(var compressor=new DeflateStream(output,CompressionLevel.Fastest,true))compressor.Write(bytes,0,bytes.Length);
  var packed=output.ToArray();return packed.Length<bytes.Length?new WirePayload{Bytes=packed,Compressed=true}:new WirePayload{Bytes=bytes};
 }
 public static byte[]? Decode(byte[] bytes,bool compressed){
  if(bytes.Length>WireTransfer.Maximum)return null;if(!compressed)return bytes;
  try{using var source=new MemoryStream(bytes);using var decoder=new DeflateStream(source,CompressionMode.Decompress);using var output=new MemoryStream();var buffer=new byte[4096];int count;
   while((count=decoder.Read(buffer,0,buffer.Length))>0){if(output.Length+count>WireTransfer.Maximum)return null;output.Write(buffer,0,count);}return output.ToArray();
  }catch(InvalidDataException){return null;}catch(IOException){return null;}
 }
}
