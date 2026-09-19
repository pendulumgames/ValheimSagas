using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimSagas;

// One coherent runtime-only world image, never persisted or exported unmasked.
// Per-cell rendering baked changing environment lighting/clouds into a quilt.
internal static class RuntimeTerrainShader {
 static readonly System.Reflection.FieldInfo materialField=AccessTools.Field(typeof(Minimap),"m_mapLargeShader");
 static byte[]? atlas;static int atlasSize,sourceId;static bool failed;
 static readonly Dictionary<(int,int),byte[]> cache=new Dictionary<(int,int),byte[]>();
 static readonly Queue<(int,int)> order=new Queue<(int,int)>();
 internal static void Clear(){atlas=null;atlasSize=0;sourceId=0;failed=false;cache.Clear();order.Clear();}
 internal static byte[]? Capture(Minimap map,int cx,int cz){
  var source=materialField.GetValue(map) as Material;
  if(source&&source.GetInstanceID()!=sourceId){Clear();sourceId=source.GetInstanceID();}
  if(failed)return null;
  try {
   if(!source||!source.shader||!source.shader.isSupported||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
    throw new InvalidOperationException("Installed large-map shader is unavailable on this graphics device.");
   if(cache.TryGetValue((cx,cz),out var pixels))return pixels;
   if(atlas==null)BuildAtlas(map,source);
   pixels=TerrainAtlasSampling.Tile(atlas!,atlasSize,map.m_textureSize*map.m_pixelSize,cx,cz);
   if(cache.Count>=128)cache.Remove(order.Dequeue());cache[(cx,cz)]=pixels;order.Enqueue((cx,cz));return pixels;
  }catch(Exception ex){failed=true;Debug.LogWarning("Valheim Sagas: coherent minimap atlas capture failed; using explicitly degraded CPU terrain fallback for this session. "+ex);return null;}
 }
 static void BuildAtlas(Minimap map,Material source){
  var previous=RenderTexture.active;bool pushed=false,begun=false;bool previousSrgb=GL.sRGBWrite;
  Material? material=null;Texture2D? readback=null;RenderTexture? target=null;Texture2D? clearCloud=null;
  try {
   // At most64MiB retained pixels; transient GPU/readback resources are released
   // after this single draw. No per-cell GPU calls or camera effects.
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
   readback=new Texture2D(atlasSize,atlasSize,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
   RenderTexture.active=target;GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
   GL.PushMatrix();pushed=true;GL.LoadOrtho();GL.Clear(true,true,Color.clear);
   if(!material.SetPass(0))throw new InvalidOperationException("Installed large-map shader rejected its render pass.");
   GL.Begin(GL.QUADS);begun=true;GL.Color(Color.white);
   GL.TexCoord2(0,0);GL.Vertex3(0,0,0);GL.TexCoord2(1,0);GL.Vertex3(1,0,0);
   GL.TexCoord2(1,1);GL.Vertex3(1,1,0);GL.TexCoord2(0,1);GL.Vertex3(0,1,0);
   GL.End();begun=false;GL.PopMatrix();pushed=false;
   readback.ReadPixels(new Rect(0,0,atlasSize,atlasSize),0,0,false);
   var pixels=readback.GetRawTextureData();
   if(!RuntimeArtPixels.HasVisibleContent(pixels))throw new InvalidOperationException("Installed large-map shader produced an empty atlas.");
   atlas=pixels;
   Debug.Log("Valheim Sagas: coherent "+atlasSize+"x"+atlasSize+" world atlas captured using '"+source.shader.name+"', static neutral daylight, animated cloud overlay disabled. Personal fog is applied before every tile export.");
  }finally{
   if(begun)GL.End();if(pushed)GL.PopMatrix();GL.sRGBWrite=previousSrgb;RenderTexture.active=previous;
   if(target){target.Release();UnityEngine.Object.Destroy(target);}if(readback)UnityEngine.Object.Destroy(readback);
   if(material)UnityEngine.Object.Destroy(material);if(clearCloud)UnityEngine.Object.Destroy(clearCloud);
  }
 }
}
