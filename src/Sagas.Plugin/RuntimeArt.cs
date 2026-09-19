using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ValheimSagas;

/// <summary>Local runtime-only artwork. Construct/call on Unity's main thread.
/// Callers MUST apply profile-sharing consent before requesting or transmitting art.
/// No installed textures or character assets are bundled with the mod.</summary>
internal sealed class RuntimeArt {
 internal sealed class Image {
  public string Id="", Kind="";
  public byte[] Png=Array.Empty<byte>();
  public int Width,Height;
 }
 const int MaximumBytes=48*1024, MaximumIcons=128;
 readonly int thread=Thread.CurrentThread.ManagedThreadId;
 readonly Action<string,Exception> warn;
 readonly Dictionary<int,Image> icons=new Dictionary<int,Image>();
 readonly Queue<int> iconOrder=new Queue<int>();
 Image? portrait;
 long portraitPlayer;
 float nextPortrait;
 public string Status {get;private set;}="waiting-for-player";
 public RuntimeArt(Action<string,Exception> warning){warn=warning;}
 void CheckThread(){if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Runtime artwork requires Unity's main thread.");}
 public void Clear(){CheckThread();icons.Clear();iconOrder.Clear();portrait=null;portraitPlayer=0;nextPortrait=0;Status="waiting-for-player";}

 public Image? TryIcon(ItemDrop.ItemData item) {
  CheckThread();
  try {
   // Installed ItemData.GetIcon selects the current variant (including modded icons).
   if(item.m_shared.m_icons==null||item.m_variant<0||item.m_variant>=item.m_shared.m_icons.Length)return null;
   var sprite=item.GetIcon();if(!sprite||!sprite.texture)return null;
   int key=sprite.GetInstanceID();if(icons.TryGetValue(key,out var known))return known;
   var rect=sprite.textureRect;
   var source=sprite.texture;
   int width=Mathf.Clamp(Mathf.RoundToInt(rect.width),1,96),height=Mathf.Clamp(Mathf.RoundToInt(rect.height),1,96);
   var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
   Image? result;
   var previous=RenderTexture.active;
   try {
    Graphics.Blit(source,rt,new Vector2(rect.width/source.width,rect.height/source.height),new Vector2(rect.x/source.width,rect.y/source.height));
    result=Encode(rt,"icon");
   }finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
   if(result==null)return null;
   if(icons.Count>=MaximumIcons)icons.Remove(iconOrder.Dequeue());
   icons[key]=result;iconOrder.Enqueue(key);return result;
  }catch(Exception ex){warn("item artwork",ex);return null;}
 }

 public Image? TryPortrait(Player player) {
  CheckThread();
  if(!player){Status="waiting-for-player";return null;}
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null){Status="no-graphics-device";return null;}
  long id=player.GetPlayerID();
  if(portraitPlayer==id&&Time.realtimeSinceStartup<nextPortrait)return portrait;
  if(portraitPlayer!=id)portrait=null;
  portraitPlayer=id;nextPortrait=Time.realtimeSinceStartup+10f;Status="rendering";
  GameObject? root=null;
  var renderers=new List<Renderer>();var fallbackMaterials=new List<Material>();
  var effectMeshes=new List<Mesh>();
  var diagnostics=new StringBuilder();
  var previous=RenderTexture.active;
  RenderTexture? target=null;
  try {
   // Select an unused active-object layer rather than assuming a mod leaves 31
   // vacant. This excludes terrain and other objects that aren't Renderers too.
   uint usedLayers=0;
   foreach(var existing in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))usedLayers|=1u<<existing.gameObject.layer;
   // Unity reserves layer31 for previews; choose an ordinary unused layer.
   int captureLayer=30;while(captureLayer>=0&&(usedLayers&(1u<<captureLayer))!=0)captureLayer--;
   if(captureLayer<0){Status="render-layer-unavailable";throw new InvalidOperationException("No isolated render layer is available for the portrait.");}
   // Recreate only transforms and visual components. Keeping native skinning
   // preserves bone matrices, bindposes and GPU vertex/index buffers; converting
   // BakeMesh into a MeshRenderer rendered static equipment but no body in the
   // observed Unity6 runtime. No Player/Animator/VisEquipment script is cloned.
   root=new GameObject("Sagas temporary portrait geometry"){hideFlags=HideFlags.HideAndDontSave};
   root.SetActive(false);
   var poses=new Dictionary<Transform,Transform>();
   int count=0,vertices=0;Bounds bounds=new Bounds();bool hasBounds=false;
   var visual=player.GetComponentInChildren<VisEquipment>();var bodySource=visual?visual.m_bodyModel:null;
   Renderer? capturedBody=null;
   foreach(var source in player.GetComponentsInChildren<Renderer>()) {
    if(!source.enabled||!source.gameObject.activeInHierarchy)continue;
    Mesh? mesh=source is SkinnedMeshRenderer skinSource?skinSource.sharedMesh:source is MeshRenderer?source.GetComponent<MeshFilter>()?.sharedMesh:null;
    if(!mesh||vertices+mesh.vertexCount>250000||count>=64)continue;
    var node=CopyPose(source.transform,root.transform,poses,captureLayer).gameObject;
    Renderer renderer;
    if(source is SkinnedMeshRenderer skin){
     var copy=node.AddComponent<SkinnedMeshRenderer>();copy.enabled=false;
     var sourceBones=skin.bones;var bones=new Transform[sourceBones.Length];
     for(int i=0;i<bones.Length;i++)bones[i]=sourceBones[i]?CopyPose(sourceBones[i],root.transform,poses,captureLayer):null!;
     copy.sharedMesh=mesh;copy.bones=bones;
     copy.rootBone=skin.rootBone?CopyPose(skin.rootBone,root.transform,poses,captureLayer):null;
     copy.quality=skin.quality;copy.localBounds=skin.localBounds;
     copy.updateWhenOffscreen=true;copy.forceMatrixRecalculationPerRender=true;copy.skinnedMotionVectors=false;
     if(diagnostics.Length<3200){int missing=0;foreach(var bone in bones)if(!bone)missing++;diagnostics.Append("bones=").Append(bones.Length).Append(" missing=").Append(missing).Append(" root=").Append((bool)copy.rootBone).Append(';');}
     for(int i=0;i<mesh.blendShapeCount;i++)copy.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i));
     renderer=copy;
    }else{
     node.AddComponent<MeshFilter>().sharedMesh=mesh;
     renderer=node.AddComponent<MeshRenderer>();
    }
    count++;vertices+=mesh.vertexCount;
    renderer.sharedMaterials=source.sharedMaterials;renderers.Add(renderer);
    if(source==bodySource)capturedBody=renderer;
    var properties=new MaterialPropertyBlock();source.GetPropertyBlock(properties);renderer.SetPropertyBlock(properties);
    for(int slot=0;slot<source.sharedMaterials.Length;slot++){var perMaterial=new MaterialPropertyBlock();source.GetPropertyBlock(perMaterial,slot);renderer.SetPropertyBlock(perMaterial,slot);}
    PortraitLighting.IsolateProbes(renderer);
    renderer.enabled=true;renderer.forceRenderingOff=false;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    // The clone retains the full original hierarchy, so live world bounds are
    // also the exact intended clone bounds. Set the clone's culling bounds
    // explicitly before its first render instead of waiting for a frame update.
    renderer.bounds=source.bounds;
    if(!hasBounds){bounds=source.bounds;hasBounds=true;}else bounds.Encapsulate(source.bounds);
    if(diagnostics.Length<3500){
     uint indices=0;for(int sub=0;sub<mesh.subMeshCount;sub++)indices+=mesh.GetIndexCount(sub);
     diagnostics.Append(source.name.Length>64?source.name.Substring(0,64):source.name).Append(": v=").Append(mesh.vertexCount).Append(" indices=").Append(indices).Append(" sub=").Append(mesh.subMeshCount).Append(" center-offset=").Append(source.bounds.center-player.transform.position).Append(" size=").Append(source.bounds.size).Append(" skin=").Append(source is SkinnedMeshRenderer).Append(';');
    }
   }
   root.SetActive(true);
   if(!capturedBody){Status="body-unavailable";warn("character portrait",new InvalidOperationException("Refusing partial equipment-only portrait: the player's VisEquipment body was missing or rejected. "+diagnostics));return portrait;}
   if(!hasBounds||vertices==0||bounds.size.sqrMagnitude<.001f){Status="waiting-for-geometry";warn("character portrait",new InvalidOperationException("No visible equipped geometry is ready. "+diagnostics));return portrait;}
   var cameraObject=new GameObject("Sagas temporary portrait camera"){hideFlags=HideFlags.HideAndDontSave};
   cameraObject.transform.SetParent(root.transform,false);
   var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
   camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.065f,0);
   // Inspected installed Jotunn RenderManager documents that Valheim material
   // shaders do not support orthographic capture. A narrow perspective keeps
   // this static2D portrait visually flat without that incompatible projection.
   camera.cullingMask=1<<captureLayer;camera.orthographic=false;camera.fieldOfView=12;camera.aspect=2f/3f;
   var aim=bounds.center;
   float distance=PortraitGeometry.RotatingCameraDistance(bounds.extents.x,bounds.extents.y,bounds.extents.z,camera.aspect,camera.fieldOfView);
   var facing=Quaternion.Euler(0,player.transform.eulerAngles.y,0);
   camera.transform.position=aim+facing*new Vector3(.2f,0,1).normalized*distance;
   camera.transform.LookAt(aim);camera.nearClipPlane=.05f;camera.farClipPlane=distance+bounds.size.magnitude+10;
   camera.allowHDR=false;camera.allowMSAA=false;camera.useOcclusionCulling=false;camera.renderingPath=RenderingPath.Forward;camera.layerCullDistances=new float[32];camera.layerCullSpherical=false;
   var lampObject=new GameObject("Sagas temporary portrait light"){hideFlags=HideFlags.HideAndDontSave};
   lampObject.transform.SetParent(root.transform,false);
   var lamp=lampObject.AddComponent<Light>();lamp.type=LightType.Directional;lamp.cullingMask=1<<captureLayer;
   lamp.intensity=.8f;lamp.color=Color.white;lamp.shadows=LightShadows.None;lamp.renderMode=LightRenderMode.ForcePixel;
   lamp.transform.rotation=Quaternion.LookRotation(facing*new Vector3(.35f,-.35f,-1));
   var fillObject=new GameObject("Sagas temporary portrait fill"){hideFlags=HideFlags.HideAndDontSave};
   fillObject.transform.SetParent(root.transform,false);
   var fill=fillObject.AddComponent<Light>();fill.type=LightType.Directional;fill.cullingMask=1<<captureLayer;
   fill.intensity=.35f;fill.color=Color.white;fill.shadows=LightShadows.None;fill.renderMode=LightRenderMode.ForcePixel;
   fill.transform.rotation=Quaternion.LookRotation(facing*new Vector3(-.5f,0,-1));
   int equippedRendererCount=renderers.Count;
   CaptureEffects(player,root,camera,captureLayer,bounds,renderers,effectMeshes,diagnostics);
   bool simplified=false,bodyVerified=false,framingChecked=false;
   // The first target is only a cheap visibility/framing probe. Re-render the
   // final pose at real higher resolution rather than enlarging a small PNG.
   foreach(int width in new[]{341,1024,768,512,341,256,192,128,96}) {
    target=RenderTexture.GetTemporary(width,width*3/2,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
    // Pooled textures can allocate lazily; IsCreated before the first bind is
    // not a failure. Explicitly request creation before checking availability.
    if(!target||(!target.IsCreated()&&!target.Create()))throw new InvalidOperationException("Private portrait render target could not be created.");
    camera.targetTexture=target;
    if(!bodyVerified){
     try{VerifyBody(camera,target,renderers,capturedBody);}
     catch(EmptyPortraitException){
      warn("character portrait body",new InvalidOperationException("Body-only native-skinned render was empty; retrying simplified materials. frame-size="+bounds.size+" body-offset="+(capturedBody.bounds.center-bounds.center)+" bones="+poses.Count+" "+diagnostics));
      if(!UseFallback(renderers,fallbackMaterials,equippedRendererCount)){Status="body-unavailable";throw;}
      simplified=true;
      try{VerifyBody(camera,target,renderers,capturedBody);}catch(EmptyPortraitException){Status="body-unavailable";throw;}
     }
     bodyVerified=true;
    }
    if(!framingChecked){
     // Probe the actual equipped silhouette instead of trusting broad animation
     // bounds. A cropped projection rerenders with more real pixels while
     // retaining the working camera position, shader inputs and native rig.
     TightenPortraitFrame(camera,target);framingChecked=true;
     VerifyBody(camera,target,renderers,capturedBody);
     camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);target=null;
     continue;
    }
    Image? result;
    try{result=EncodePortrait(camera,target,bodyVerified);}
    catch(EmptyPortraitException) when(!simplified){
     warn("character portrait original shader",new InvalidOperationException("Original material capture contained no visible pixels; retrying with a simplified texture material. layer="+captureLayer+" size="+bounds.size+" renderers="+renderers.Count+" "+diagnostics));
     if(!UseFallback(renderers,fallbackMaterials,equippedRendererCount)){Status="capture-empty";throw;}
     simplified=true;VerifyBody(camera,target,renderers,capturedBody);result=EncodePortrait(camera,target,bodyVerified);
    }
    camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);target=null;
    if(result!=null){portrait=result;Status=simplified?"simplified":"ready";nextPortrait=Time.realtimeSinceStartup+(simplified?30f:60f);Debug.Log("Valheim Sagas portrait: "+Status+", body verified, neutral studio lighting, background-difference alpha, "+result.Width+"x"+result.Height+", "+result.Png.Length+" bytes, "+renderers.Count+" equipped meshes, size="+bounds.size+", layer="+captureLayer+". "+diagnostics);return result;}
   }
   Status="image-too-large";warn("character portrait",new InvalidOperationException("Portrait exceeds the bounded portrait upload size after downscaling."));
   return portrait;
  }catch(Exception ex){if(Status=="rendering")Status=ex is EmptyPortraitException?"capture-empty":"capture-failed";warn("character portrait",new InvalidOperationException("Portrait status="+Status+". "+diagnostics,ex));return portrait;}
  finally {
   RenderTexture.active=previous;if(target)RenderTexture.ReleaseTemporary(target);
   if(root){root.SetActive(false);UnityEngine.Object.Destroy(root);}
   foreach(var material in fallbackMaterials)UnityEngine.Object.Destroy(material);
   foreach(var mesh in effectMeshes)UnityEngine.Object.Destroy(mesh);
  }
 }

 // Freeze attached visual effects into inert meshes. Never clone effect scripts,
 // advance live particle simulation or change the source renderer/light. Baking
 // both matte passes from the same snapshot avoids flickering opacity edges.
 void CaptureEffects(Player player,GameObject root,Camera camera,int layer,Bounds bodyBounds,List<Renderer> renderers,List<Mesh> meshes,StringBuilder diagnostics){
  int captured=0,vertices=0,skipped=0,lights=0,attempts=0;
  var allowed=bodyBounds;allowed.Expand(4f);
  var poses=new Dictionary<Transform,Transform>();
  bool InBounds(Bounds b)=>allowed.Contains(b.min)&&allowed.Contains(b.max)&&b.size.sqrMagnitude>0;
  void Add(Renderer source,Mesh mesh,Material? material,bool worldSpace){
   if(!mesh||mesh.vertexCount==0||!material)return;
   if(captured>=24||mesh.vertexCount>32768-vertices){skipped++;return;}
   GameObject node;
   if(worldSpace){node=new GameObject("Sagas frozen equipment effect"){hideFlags=HideFlags.HideAndDontSave,layer=layer};node.transform.SetParent(root.transform,false);}
   else node=CopyPose(source.transform,root.transform,poses,layer).gameObject;
   // A trail and its particles must have distinct renderer components even
   // when the source shares one transform.
   if(node.GetComponent<MeshRenderer>()){
    var child=new GameObject("Sagas frozen effect trail"){hideFlags=HideFlags.HideAndDontSave,layer=layer};child.transform.SetParent(node.transform,false);node=child;
   }
   node.AddComponent<MeshFilter>().sharedMesh=mesh;
   var copy=node.AddComponent<MeshRenderer>();copy.sharedMaterial=material;
   var properties=new MaterialPropertyBlock();source.GetPropertyBlock(properties);copy.SetPropertyBlock(properties);
   var perMaterial=new MaterialPropertyBlock();source.GetPropertyBlock(perMaterial,0);copy.SetPropertyBlock(perMaterial,0);
   PortraitLighting.IsolateProbes(copy);
   copy.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;copy.receiveShadows=false;copy.bounds=source.bounds;
   renderers.Add(copy);captured++;vertices+=mesh.vertexCount;
  }
  Mesh NewMesh(){var mesh=new Mesh{name="Sagas frozen effect mesh",hideFlags=HideFlags.HideAndDontSave};meshes.Add(mesh);return mesh;}
  foreach(var source in player.GetComponentsInChildren<ParticleSystemRenderer>()){
   if(captured>=24||attempts++>=32)break;
   var system=source.GetComponent<ParticleSystem>();
   if(!source.enabled||!source.gameObject.activeInHierarchy||!system||system.particleCount==0)continue;
   if(system.particleCount>2048||!InBounds(source.bounds)){skipped++;continue;}
   // Bound mesh-particle expansion before entering native BakeMesh, not only
   // after allocating its output. Billboard quads need four vertices each.
   long perParticle=4;
   if(source.renderMode==ParticleSystemRenderMode.Mesh){
    if(source.meshCount>16){skipped++;continue;}
    var sourceMeshes=new Mesh[source.meshCount];source.GetMeshes(sourceMeshes);
    foreach(var sourceMesh in sourceMeshes)if(sourceMesh)perParticle=Math.Max(perParticle,sourceMesh.vertexCount);
   }
   if(perParticle*system.particleCount>32768-vertices){skipped++;continue;}
   try{
    // Installed Unity6 options explicitly bake translation AND rotation/scale;
    // the destination stays at identity in world space, avoiding double scale.
    var options=ParticleSystemBakeMeshOptions.BakeRotationAndScale|ParticleSystemBakeMeshOptions.BakePosition;
    if(source.renderMode!=ParticleSystemRenderMode.None){var mesh=NewMesh();source.BakeMesh(mesh,camera,options);Add(source,mesh,source.sharedMaterial,true);}
    // Particle trail history has no cheap reliable pre-bake vertex budget.
    // Preserve its particles; skip that unbounded history in this still image.
    if(system.trails.enabled)skipped++;
   }catch(Exception ex){skipped++;warn("portrait attached particle snapshot",ex);}
  }
  foreach(var source in player.GetComponentsInChildren<TrailRenderer>()){
   if(captured>=24||attempts++>=32)break;
   if(!source.enabled||!source.gameObject.activeInHierarchy||source.positionCount<2)continue;
   long trailVertices=((long)source.positionCount*(2L+source.numCornerVertices)+2L*source.numCapVertices)*2;
   if(source.positionCount>2048||trailVertices>32768-vertices||!InBounds(source.bounds)){skipped++;continue;}
   try{var mesh=NewMesh();source.BakeMesh(mesh,camera,false);Add(source,mesh,source.sharedMaterial,false);}
   catch(Exception ex){skipped++;warn("portrait attached trail snapshot",ex);}
  }
  foreach(var source in player.GetComponentsInChildren<Light>()){
   if(lights>=8)break;
   if(!source.enabled||!source.gameObject.activeInHierarchy||source.intensity<=0||!allowed.Contains(source.transform.position)||(source.type!=LightType.Point&&source.type!=LightType.Spot))continue;
   // Always freeze attached lights. The render scope excludes ALL original
   // scene lights from the capture layer, preventing sunlight and duplicates.
   var node=new GameObject("Sagas frozen equipment light"){hideFlags=HideFlags.HideAndDontSave,layer=layer};node.transform.SetParent(root.transform,false);
   node.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
   var copy=node.AddComponent<Light>();copy.type=source.type;copy.color=source.color;copy.intensity=Mathf.Clamp(source.intensity,0,8);copy.range=Mathf.Clamp(source.range,0,20);
   copy.useColorTemperature=source.useColorTemperature;copy.colorTemperature=source.colorTemperature;
   copy.spotAngle=source.spotAngle;copy.innerSpotAngle=source.innerSpotAngle;copy.cullingMask=1<<layer;copy.shadows=LightShadows.None;lights++;
  }
  diagnostics.Append(" frozen-effects=").Append(captured).Append(" effect-vertices=").Append(vertices).Append(" attached-lights=").Append(lights).Append(" skipped-effects=").Append(skipped).Append(';');
 }

 static void TightenPortraitFrame(Camera camera,RenderTexture target){
  var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
  try{
   ReadPortraitMatte(camera,target,texture);
   if(!RuntimeArtPixels.TryPortraitFrame(texture.GetRawTextureData(),target.width,target.height,out float cx,out float cy,out float scale))return;
   // In clip space x'= (x-cx*w)/scale. This is an exact off-axis
   // projection crop, independent of the depth of cloak, bow or shield.
   var crop=Matrix4x4.identity;crop.m00=crop.m11=1/scale;crop.m03=-cx/scale;crop.m13=-cy/scale;
   camera.projectionMatrix=crop*camera.projectionMatrix;
   Debug.Log("Valheim Sagas portrait framing: actual silhouette zoom="+(1/scale).ToString("F2")+", padded viewport center="+cx.ToString("F3")+","+cy.ToString("F3"));
  }finally{UnityEngine.Object.Destroy(texture);}
 }

 static void VerifyBody(Camera camera,RenderTexture target,List<Renderer> renderers,Renderer body){
  // A separate body-only pass prevents a hammer/shield from satisfying the
  // visibility check. Only inert capture renderers are toggled, never live gear.
  var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
  try{
   foreach(var renderer in renderers)renderer.enabled=renderer==body;
   ReadPortraitMatte(camera,target,texture);
   var pixels=texture.GetRawTextureData();
   if(!RuntimeArtPixels.HasVisibleContent(pixels))throw new EmptyPortraitException("No visible player body in isolated body pass; equipment-only portrait rejected. visible="+RuntimeArtPixels.CountVisible(pixels)+" rgb-variation="+RuntimeArtPixels.CountRgbVariation(pixels)+" viewport-center="+camera.WorldToViewportPoint(body.bounds.center)+" body-size="+body.bounds.size);
  }finally{foreach(var renderer in renderers)renderer.enabled=true;UnityEngine.Object.Destroy(texture);}
 }

 // Copy ancestors too: importer scales can be split across parent and bone
 // transforms (including mirrored/nonuniform scales). Decomposing a world
 // matrix loses shear. Identical local TRS chains preserve the exact matrices.
 // No source Transform is reparented, changed or passed as a live bone reference.
 static Transform CopyPose(Transform source,Transform root,Dictionary<Transform,Transform> copies,int layer,int depth=0){
  if(copies.TryGetValue(source,out var known))return known;
  if(copies.Count>=1024||depth>=128)throw new InvalidOperationException("Portrait transform limit exceeded.");
  var parent=source.parent?CopyPose(source.parent,root,copies,layer,depth+1):root;
  if(copies.Count>=1024)throw new InvalidOperationException("Portrait transform limit exceeded.");
  var node=new GameObject("Sagas captured pose"){hideFlags=HideFlags.HideAndDontSave,layer=layer};
  var copy=node.transform;copy.SetParent(parent,false);
  copy.localPosition=source.localPosition;copy.localRotation=source.localRotation;copy.localScale=source.localScale;
  copies.Add(source,copy);return copy;
 }

 // Installed Custom/Player FORWARD passes have ColorWriteMask14 (RGB only).
 // Observed0.2.6 body pixels therefore retained the transparent clear alpha.
 // Recover actual coverage from two backgrounds instead
 // of treating material alpha as visibility or making a whole rectangle opaque.
 // Cb = alpha*foreground, Cw = Cb + (1-alpha). Identical shader output over
 // both backgrounds is opaque; unchanged background remains transparent.
 // Both renders occur synchronously with unchanged pose/time and private camera.
 static void ReadPortraitMatte(Camera camera,RenderTexture target,Texture2D texture){
  var oldColor=camera.backgroundColor;
  try{
   camera.backgroundColor=new Color(0,0,0,0);PortraitLighting.Render(camera);RenderTexture.active=target;
   texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);var black=texture.GetRawTextureData();
   camera.backgroundColor=new Color(1,1,1,0);PortraitLighting.Render(camera);RenderTexture.active=target;
   texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);var white=texture.GetRawTextureData();
   RuntimeArtPixels.ReconstructMatte(black,white,QualitySettings.activeColorSpace==ColorSpace.Linear);
   texture.LoadRawTextureData(black);
  }finally{camera.backgroundColor=oldColor;}
 }
 static Image? EncodePortrait(Camera camera,RenderTexture target,bool bodyVerified){
  var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
  try{
   ReadPortraitMatte(camera,target,texture);var pixels=texture.GetRawTextureData();
   if(!RuntimeArtPixels.CanPublishPortrait(bodyVerified,pixels))throw new EmptyPortraitException("Portrait has no verified visible body or sufficient geometry after alpha reconstruction ("+RuntimeArtPixels.CountVisible(pixels)+" of"+(pixels.Length/4)+" pixels).");
   var bytes=ImageConversion.EncodeToPNG(texture);if(bytes.Length>SagaService.MaximumPortraitBytes)return null;
   using(var hash=SHA256.Create())return new Image{Id=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(),Kind="portrait",Png=bytes,Width=target.width,Height=target.height};
  }finally{UnityEngine.Object.Destroy(texture);}
 }

 static Image? Encode(RenderTexture source,string kind) {
  var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
  try {
   RenderTexture.active=source;texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0,false);
   var bytes=ImageConversion.EncodeToPNG(texture);if(bytes.Length>MaximumBytes)return null;
   using(var hash=SHA256.Create())return new Image{Id=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(),Kind=kind,Png=bytes,Width=source.width,Height=source.height};
  }finally{UnityEngine.Object.Destroy(texture);}
 }

 sealed class EmptyPortraitException:Exception {public EmptyPortraitException(string message):base(message){}}
 static bool UseFallback(List<Renderer> renderers,List<Material> owned,int equippedCount){
  var shader=Shader.Find("Unlit/Texture");if(!shader||!shader.isSupported)shader=Shader.Find("Standard");
  if(!shader||!shader.isSupported)return false;
  // Transparent effect shaders must survive fallback; replacing a glow quad
  // with an opaque texture shader would cover the character with rectangles.
  for(int index=0;index<Math.Min(equippedCount,renderers.Count);index++){
   var renderer=renderers[index];
   var originals=renderer.sharedMaterials;var replacements=new Material[originals.Length];
   for(int i=0;i<originals.Length;i++){
    var source=originals[i];var material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};owned.Add(material);replacements[i]=material;
    if(source){material.mainTexture=source.mainTexture;material.mainTextureScale=source.mainTextureScale;material.mainTextureOffset=source.mainTextureOffset;
     if(material.HasProperty("_Color")){var color=source.HasProperty("_Color")?source.GetColor("_Color"):Color.white;if(source.HasProperty("_SkinColor"))color*=source.GetColor("_SkinColor");color.a=1;material.SetColor("_Color",color);}}
    if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",0);
    renderer.SetPropertyBlock(null,i);
   }
   renderer.SetPropertyBlock(null);renderer.sharedMaterials=replacements;
  }
  return true;
 }

 public static Dictionary<string,string> EffectiveResistances(Player player) {
  // Character.GetDamageModifiers applies armor then current status-effect mods;
  // Harmony integrations remain active because this calls the live public API.
  var modifiers=player.GetDamageModifiers();var result=new Dictionary<string,string>();
  foreach(var type in new[]{HitData.DamageType.Blunt,HitData.DamageType.Slash,HitData.DamageType.Pierce,HitData.DamageType.Fire,HitData.DamageType.Frost,HitData.DamageType.Lightning,HitData.DamageType.Poison,HitData.DamageType.Spirit,HitData.DamageType.Chop,HitData.DamageType.Pickaxe})
   result[type.ToString()]=modifiers.GetModifier(type).ToString();
  return result;
 }
}
