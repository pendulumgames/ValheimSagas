using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch=System.Diagnostics.Stopwatch;

namespace ValheimSagas;

// One coherent runtime-only world image, never persisted or exported unmasked.
// Per-cell rendering baked changing environment lighting/clouds into a quilt.
internal static class RuntimeTerrainShader {
 static readonly System.Reflection.FieldInfo materialField=AccessTools.Field(typeof(Minimap),"m_mapLargeShader");
 static StripAtlas? atlas;static CaptureJob? job;static Minimap? requestedMap;static Material? requestedSource;static int inFlight;
 internal static bool Pending=>job!=null||requestedMap;static int atlasSize,sourceId;static bool failed;
 static readonly Dictionary<(int,int),byte[]> cache=new Dictionary<(int,int),byte[]>();
 static readonly Queue<(int,int)> order=new Queue<(int,int)>();
 internal static void Clear(){job?.Retire();job=null;requestedMap=null;requestedSource=null;atlas=null;atlasSize=0;sourceId=0;failed=false;cache.Clear();order.Clear();}
 internal static byte[]? Capture(Minimap map,int cx,int cz){
  var source=materialField.GetValue(map) as Material;
  if(source&&source.GetInstanceID()!=sourceId){Clear();sourceId=source.GetInstanceID();}
  if(failed)return null;
  try {
   if(!source||!source.shader||!source.shader.isSupported||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
    throw new InvalidOperationException("Installed large-map shader is unavailable on this graphics device.");
   if(cache.TryGetValue((cx,cz),out var pixels))return pixels;
   if(atlas==null){if(job==null&&!requestedMap){requestedMap=map;requestedSource=source;}return null;}
   pixels=atlas.Tile(map.m_textureSize*map.m_pixelSize,cx,cz);
   if(cache.Count>=128)cache.Remove(order.Dequeue());cache[(cx,cz)]=pixels;order.Enqueue((cx,cz));return pixels;
  }catch(Exception ex){failed=true;Debug.LogWarning("Valheim Sagas: coherent minimap atlas capture failed; using explicitly degraded CPU terrain fallback for this session. "+ex);return null;}
 }
 internal static void Pump(){
  if(job==null&&requestedMap&&inFlight==0){var map=requestedMap;var source=requestedSource;requestedMap=null;requestedSource=null;try{var timer=Stopwatch.StartNew();job=BuildAtlas(map!,source!);Debug.Log("Sagas atlas render/setup: "+timer.Elapsed.TotalMilliseconds.ToString("F1")+" ms; pixel readback continues in bounded asynchronous strips.");}catch(Exception e){failed=true;Debug.LogWarning("Sagas atlas preparation failed; using degraded terrain: "+e);}}
  var current=job;if(current==null)return;
  if(current.Error!=null){failed=true;Debug.LogWarning("Sagas atlas asynchronous readback failed; using degraded terrain: "+current.Error);current.Retire();job=null;return;}
  if(current.Pixels.Complete){if(current.Pixels.Visible){atlas=current.Pixels;Debug.Log("Sagas coherent atlas ready: "+atlas.Size+"x"+atlas.Size+", asynchronous strips; max readback callback="+current.MaximumCopyMs.ToString("F1")+" ms. Personal fog applied before export.");}else{failed=true;Debug.LogWarning("Sagas atlas was empty; using degraded terrain.");}current.Retire();job=null;return;}
  current.Submit();
 }
 sealed class CaptureJob {
  internal readonly StripAtlas Pixels;RenderTexture? target;Material? material;Texture2D? cloud;internal Exception? Error;internal double MaximumCopyMs;bool pending,retired;int row;float submittedAt;readonly ReadbackLifetime lifetime=new ReadbackLifetime();
  internal CaptureJob(RenderTexture target,Material material,Texture2D cloud,int size){this.target=target;this.material=material;this.cloud=cloud;Pixels=new StripAtlas(size,32);}
  internal void Submit(){if(pending){if(Time.realtimeSinceStartup-submittedAt>15)Error=new TimeoutException("Atlas readback timed out; resources retained until callback.");return;}if(retired)return;pending=true;submittedAt=Time.realtimeSinceStartup;inFlight++;lifetime.Begin();try{AsyncGPUReadback.Request(target!,0,0,Pixels.Size,row,Pixels.Rows,0,1,TextureFormat.RGBA32,Complete);}catch(Exception e){pending=false;inFlight--;lifetime.Complete();Error=e;}}
  void Complete(AsyncGPUReadbackRequest request){var timer=Stopwatch.StartNew();try{if(!retired){if(request.hasError)throw new InvalidOperationException("Atlas strip readback failed.");var data=request.GetData<byte>();var bytes=new byte[data.Length];data.CopyTo(bytes);Pixels.Add(row,bytes);row+=Pixels.Rows;}}catch(Exception e){Error=e;}finally{MaximumCopyMs=Math.Max(MaximumCopyMs,timer.Elapsed.TotalMilliseconds);pending=false;inFlight--;lifetime.Complete();}}
  internal void Retire(){retired=true;lifetime.Retire(Release);}
  void Release(){if(target){target.Release();UnityEngine.Object.Destroy(target);target=null;}if(material){UnityEngine.Object.Destroy(material);material=null;}if(cloud){UnityEngine.Object.Destroy(cloud);cloud=null;}}
 }
 static CaptureJob BuildAtlas(Minimap map,Material source){
  var previous=RenderTexture.active;bool pushed=false,begun=false;bool previousSrgb=GL.sRGBWrite;
  Material? material=null;RenderTexture? target=null;Texture2D? clearCloud=null;bool transferred=false;
  try {
   if(!SystemInfo.supportsAsyncGPUReadback)throw new NotSupportedException("Asynchronous atlas readback unavailable.");
   // One coherent draw; bounded strips read the same retained target across frames.
   atlasSize=Math.Min(4096,Math.Min(SystemInfo.maxTextureSize,Math.Max(1024,map.m_textureSize*2)));
   material=new Material(source){hideFlags=HideFlags.HideAndDontSave};
   material.SetTexture("_FogTex",Texture2D.blackTexture);material.SetFloat("_SharedFade",0);
   // Confirmed in installed Custom/mapshader property table and DXBC bindings:
   // cloud alpha blends over known terrain independently from exploration fog.
   clearCloud=new Texture2D(1,1,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};clearCloud.SetPixel(0,0,Color.clear);clearCloud.Apply(false,false);
   material.SetTexture("_CloudTex",clearCloud);
   // Selected neutral daylight, consistent across clients and restarts. These
   // are local overrides of inspected shader uniforms, never Shader.SetGlobal*.
   material.SetVector("_SunDir",new Vector4(.3f,1,.3f,0));
   material.SetColor("_SunColor",new Color(.8f,.8f,.8f,1));
   material.SetColor("_AmbientColor",new Color(.2f,.2f,.2f,1));
   material.SetColor("_SunFogColor",Color.white);material.SetVector("_CloudOffset",Vector4.zero);
   //0.1 is installed Minimap's default large-map zoom. The shader product
   // zoom*pixelSize stays200, preserving its world quantization grid.
   material.SetFloat("_zoom",.1f);material.SetFloat("_pixelSize",2000);material.SetVector("_mapCenter",Vector4.zero);
   material.SetTextureScale("_MainTex",Vector2.one);material.SetTextureOffset("_MainTex",Vector2.zero);
   material.DisableKeyword("UNITY_UI_CLIP_RECT");material.DisableKeyword("UNITY_UI_ALPHACLIP");
   if(material.HasProperty("_StencilComp"))material.SetInt("_StencilComp",8);
   if(material.HasProperty("_Stencil"))material.SetInt("_Stencil",0);
   if(material.HasProperty("_ColorMask"))material.SetInt("_ColorMask",15);
   if(material.HasProperty("_ClipRect"))material.SetVector("_ClipRect",new Vector4(-100000,-100000,100000,100000));
   target=new RenderTexture(atlasSize,atlasSize,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
   if(!target.Create())throw new InvalidOperationException("Could not create the private minimap atlas target.");
   RenderTexture.active=target;GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
   GL.PushMatrix();pushed=true;GL.LoadOrtho();GL.Clear(true,true,Color.clear);
   if(!material.SetPass(0))throw new InvalidOperationException("Installed large-map shader rejected its render pass.");
   GL.Begin(GL.QUADS);begun=true;GL.Color(Color.white);
   GL.TexCoord2(0,0);GL.Vertex3(0,0,0);GL.TexCoord2(1,0);GL.Vertex3(1,0,0);
   GL.TexCoord2(1,1);GL.Vertex3(1,1,0);GL.TexCoord2(0,1);GL.Vertex3(0,1,0);
   GL.End();begun=false;GL.PopMatrix();pushed=false;
   var capture=new CaptureJob(target,material,clearCloud,atlasSize);transferred=true;return capture;
  }finally{
   if(begun)GL.End();if(pushed)GL.PopMatrix();GL.sRGBWrite=previousSrgb;RenderTexture.active=previous;
   if(!transferred){if(target){target.Release();UnityEngine.Object.Destroy(target);}if(material)UnityEngine.Object.Destroy(material);if(clearCloud)UnityEngine.Object.Destroy(clearCloud);}
  }
 }
}
