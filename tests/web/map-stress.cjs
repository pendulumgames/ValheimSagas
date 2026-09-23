// Isolated synthetic renderer benchmark. No server, saves, accounts or game assets.
// Run: node tests/web/map-stress.cjs [output.json]
const {chromium}=require('playwright'),fs=require('node:fs'),path=require('node:path');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.SAGAS_BROWSER_EXECUTABLE||(process.platform==='win32'?'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe':undefined)});
 try {
  const page=await browser.newPage({viewport:{width:2560,height:1440},deviceScaleFactor:1});
  await page.addInitScript(()=>{window.setInterval=()=>0;window.requestAnimationFrame=()=>0;window.ResizeObserver=class{observe(){}};});
  await page.route('**/*',route=>{
   const name=new URL(route.request().url()).pathname;
   if(!['/','/app.js','/style.css','/favicon.svg'].includes(name))return route.fulfill({status:404,body:'Synthetic benchmark: network disabled'});
   return route.fulfill({path:path.resolve('src/Sagas.Web',name==='/'?'index.html':name.slice(1)),contentType:name.endsWith('.js')?'application/javascript':name.endsWith('.css')?'text/css':name.endsWith('.svg')?'image/svg+xml':'text/html'});
  });
  await page.goto('http://sagas-benchmark.invalid');
  const setup=await page.evaluate(()=>{
   document.querySelectorAll('dialog[open]').forEach(d=>d.close());
   document.body.dataset.page='world';$('worldPage').hidden=false;
   canvas.style.cssText='width:2000px;height:1000px;max-width:none';
   state.busy=true;state.filter.world='synthetic-stress';state.data={world:state.filter.world,events:[],players:[]};state.fit=false;
   $('personalPinsLayer').checked=$('gamePinsLayer').checked=true;$('killLayer').checked=$('lootLayer').checked=false;
   // Current stream overview format. Exact exploration is supplied separately;
   // detailed 64x64 tiles are fetched only around the viewport by the application.
   const cells=[];
   const explorationMask=btoa(String.fromCharCode(255).repeat(512));
   for(let z=-157;z<157;z++)for(let x=-157;x<157;x++)if(Math.hypot((x+.5)*64,(z+.5)*64)<=10000){const n=cells.length,pixel=String.fromCharCode(n&255,(n>>8)&255,(n>>16)&255,255);cells.push({x,z,biome:'Meadows',terrainPixels:btoa(pixel.repeat(16)),explorationMask});}
   state.map={cells,cellSize:64};state.mapCells=new Map(cells.map(c=>[c.x+','+c.z,c]));state.mapKeys=new Set(state.mapCells.keys());
   window.stressZones=[];for(let z=-10000;z<10000;z+=100)for(let x=-10000;x<10000;x+=100)stressZones.push({minX:x,maxX:x+100,minZ:z,maxZ:z+100,level:2,color:'#bb8855'});
   window.stressPins=[];let seed=73;const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
   for(let i=0;i<1000;i++){const angle=random()*Math.PI*2,radius=Math.sqrt(random())*9800;stressPins.push({x:Math.cos(angle)*radius,z:Math.sin(angle)*radius,personal:i%2===0,name:'Synthetic pin '+i,playerId:'fixture-'+i%20,owner:'Fixture '+i%20,type:'Icon0'});}
   pinContext=mapDetailsContext();view.x=view.z=0;view.scale=.04;
   const original=terrainTile,cacheSet=tileCache.set.bind(tileCache);window.stressMetrics={calls:0,misses:0};terrainTile=function(c){stressMetrics.calls++;return original(c);};tileCache.set=function(key,value){stressMetrics.misses++;return cacheSet(key,value);};
   window.stressPrepareStart=performance.now();terrainAtlas.replace(cells);
   return {cells:cells.length,pins:stressPins.length,zones:stressZones.length,synthetic:true,viewport:'2560x1440',canvas:'2000x1000',terrainSide:4,exactExplorationMask:true};
  });
  await page.waitForFunction(()=>!terrainAtlas.pending,null,{polling:50,timeout:45000});
  setup.prepareMs=await page.evaluate(()=>Math.round(performance.now()-stressPrepareStart));
  setup.chunks=await page.evaluate(()=>terrainAtlas.chunks.size);
  console.log(JSON.stringify(setup));const results=[];
  const icons=await page.evaluate(()=>{view.width=2000;view.height=1000;canvas.width=2000;canvas.height=1000;return [0,400,1000].map(count=>{mapPins=stressPins.slice(0,count);const ms=[];for(let i=0;i<10;i++){const start=performance.now();drawMapPins(ctx);ms.push(Math.round((performance.now()-start)*10)/10);}return {count,groups:pinLayout.length,ms};});});console.log(JSON.stringify({icons}));
  for(const config of [{sls:false,pins:1000},{sls:false,pins:400},{sls:false,pins:0},{sls:true,pins:1000},{sls:true,pins:1000,events:5000}]){
   let timer;const result=await Promise.race([page.evaluate(async({sls,pins,events=0})=>{
    mapPins=stressPins.slice(0,pins);state.map.sls=sls?{installed:true,zoneScalingEnabled:true,overlayEnabled:true,aboveFog:false,opacity:.2,zones:stressZones}:null;$('slsLayer').checked=sls;
    state.data.events=Array.from({length:events},(_,i)=>({kind:i%2?'kill':'collect',x:stressPins[i%1000].x,z:stressPins[i%1000].z,amount:1,playerId:'fixture-0'}));$('killLayer').checked=$('lootLayer').checked=events>0;$('lootSource').value='collect';
    const frames=[];
    for(let i=0;i<12;i++){await new Promise(resolve=>setTimeout(resolve,17));stressMetrics.calls=stressMetrics.misses=0;view.scale=.045+i*.0005;view.x=i*50;const start=performance.now();drawMap();frames.push({ms:Math.round((performance.now()-start)*10)/10,...stressMetrics,groups:pinLayout.length});}
    // Isolate pin visibility, exploration checks, clustering and drawing.
    const iconSamples=[];for(let i=0;i<5;i++){const start=performance.now();drawMapPins(ctx);iconSamples.push(performance.now()-start);}
    return {sls,pins,events,frames,iconOnlyMs:iconSamples.map(v=>Math.round(v*10)/10)};
   },config),new Promise(resolve=>{timer=setTimeout(()=>resolve({...config,timeoutMs:45000}),45000);})]);clearTimeout(timer);results.push(result);console.log(JSON.stringify(result));if(result.timeoutMs)break;
  }
  const output={setup,browser:await browser.version(),icons,results,limitations:['Synthetic current-format overview and exact masks; HTTP transfer and detail loading tested separately.','Headless browser timings are not real-user FPS or GPU benchmarks.','Final case includes 5000 events across1000 positions; half personal and half game pins; no external icon image requests.','Twelve pan/zoom redraws per case after bounded overview preparation, spaced17ms to let the compositor progress; 45 second guard.']};
  if(process.argv[2])fs.writeFileSync(process.argv[2],JSON.stringify(output,null,2));console.log(JSON.stringify(setup));
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
