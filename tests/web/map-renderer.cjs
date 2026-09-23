const {chromium}=require('playwright'),fs=require('node:fs'),assert=require('node:assert/strict');
const source=fs.readFileSync(require('node:path').resolve(__dirname,'../../src/Sagas.Web/app.js'),'utf8');
const code=source.slice(source.indexOf('class MapTerrain'),source.indexOf('let mapMetaDue'))+'\n'+source.split('\n').find(line=>line.startsWith('function terrainTile('));
(async()=>{const browser=await chromium.launch({headless:true,executablePath:process.env.SAGAS_BROWSER_EXECUTABLE||(process.platform==='win32'?'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe':undefined)});try{const page=await browser.newPage();const result=await page.evaluate(async code=>{
 const biomes={unknown:'#123456'},tileCache=new Map(),requestMapDraw=()=>{},view={width:256,height:256,x:0,z:0,scale:1},screen=(x,z)=>[x+128,128-z];
 return await eval(code+`(async()=>{
 const checks={};const bytes=new Uint8Array(64*64*4),mask=new Uint8Array(512);for(let z=32;z<64;z++)for(let x=0;x<64;x++){const p=z*64+x;bytes[p*4]=200;bytes[p*4+3]=255;mask[p>>3]|=1<<(p&7);}
 const cell={x:0,z:0,terrainPixels:btoa(String.fromCharCode(...bytes)),explorationMask:btoa(String.fromCharCode(...mask))};
 terrainAtlas.replace([cell]);while(terrainAtlas.pending)await new Promise(r=>setTimeout(r,1));
 const canvas=document.createElement('canvas');canvas.width=canvas.height=256;const ctx=canvas.getContext('2d');terrainAtlas.draw(ctx);let rgba=ctx.getImageData(0,0,256,256).data;const alpha=(x,z)=>rgba[((128-z)*256+128+x)*4+3];
 checks.northSouth=alpha(10,50)===255&&alpha(10,10)===0;checks.mask=terrainAtlas.known(cell,10,50)&&!terrainAtlas.known(cell,10,10);checks.chunkCount=terrainAtlas.chunks.size===1;
 bytes[(32*64+10)*4+3]=0;const partial={...cell,terrainPixels:btoa(String.fromCharCode(...bytes))};terrainAtlas.accept([partial]);while(terrainAtlas.pending)await new Promise(r=>setTimeout(r,1));ctx.clearRect(0,0,256,256);terrainAtlas.draw(ctx);rgba=ctx.getImageData(0,0,256,256).data;checks.conservative=alpha(10,40)===0&&alpha(20,40)===255;
 terrainAtlas.addDetails([partial],64);ctx.clearRect(0,0,256,256);terrainAtlas.draw(ctx);rgba=ctx.getImageData(0,0,256,256).data;checks.preciseDetail=alpha(10,40)===255&&alpha(10,32)===0;
 terrainAtlas.accept([cell]);checks.invalidateDetail=terrainAtlas.details.size===0;terrainAtlas.reset();checks.reset=terrainAtlas.chunks.size===0&&terrainAtlas.details.size===0&&terrainAtlas.masks.size===0;
 const negative={...cell,x:-1,z:-1};terrainAtlas.replace([negative]);while(terrainAtlas.pending)await new Promise(r=>setTimeout(r,1));checks.negative=terrainAtlas.chunks.has('-1,-1');
 return checks;})()`);
 },code);for(const [key,value]of Object.entries(result))assert(value,key);console.log('PASS '+Object.keys(result).length+' chunk orientation, exact fog, conservative overview, detail, invalidation and negative-coordinate checks.');}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1});
