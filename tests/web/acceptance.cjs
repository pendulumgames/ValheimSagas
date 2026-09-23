const {revealAtlas}=require('./atlas-controls.cjs');
/* Requires: dotnet run --project src/Sagas.DevHost -c Release
   npm ci --prefix tests/web; npm test --prefix tests/web
   Uses installed Edge on Windows; set SAGAS_BROWSER_EXECUTABLE or install Playwright Chromium elsewhere.
   This is synthetic HTTP/browser verification, never in-game verification. */
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const url = process.env.SAGAS_TEST_URL || 'http://127.0.0.1:9847/';
if (!/^http:\/\/(127\.0\.0\.1|localhost):\d+\/$/.test(url)) throw Error('Tests require an isolated loopback DevHost.');
const executablePath = process.env.SAGAS_BROWSER_EXECUTABLE || (process.platform === 'win32' ? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' : undefined);
const token = 'synthetic-development-token-only';
const output = path.resolve(__dirname, '../../.dev/qa');
fs.mkdirSync(output, { recursive: true });
(async () => {
 const browser = await chromium.launch({ headless: true, executablePath });
 const page = await browser.newPage({ viewport: { width: 1440, height: 1050 }, hasTouch: true });
 const errors = [];
 page.on('pageerror', e => errors.push(e.message));
 page.on('console', m => { if(m.type()==='error'&&!m.text().includes('favicon.ico')) errors.push(m.text()); });
 const requests = [];
 page.on('request', r => { if(r.url().includes('/api/'))requests.push(r); });
 try {
  await page.goto(url);
  await page.locator('#access').waitFor({ state: 'visible' });
  assert.equal(requests.filter(r=>r.url().includes('/api/state')).length,0,'Private browser does not request telemetry before token');
  await page.locator('#token').fill(token);
  await page.locator('#accessForm button[type=submit]').click();
  await page.waitForFunction(() => document.querySelector('#connection').textContent.includes('Connected'));
  assert.equal(await page.locator('#fixture').isVisible(), true, 'Fixture clearly labelled');
  assert.equal(await page.evaluate(()=>{const ids=[...document.querySelectorAll('[id]')].map(e=>e.id);return ids.length===new Set(ids).size;}),true,'Combined pages have no duplicate IDs');
  assert.equal(await page.locator('#worldPage').isVisible(),true,'World combines atlas and statistics by default');
  assert.equal(await page.locator('.masthead nav a').count(),4,'World, Vikings, leaderboard and server saga navigation');
  assert.equal(await page.locator('#kills').textContent(),'60','Overview defaults all history and everyone');
  assert.equal(await page.locator('#worldLabel').isVisible(),true,'Multiple synthetic worlds remain selectable');const singleWorldHidden=await page.evaluate(()=>{const worlds=state.data.worlds;state.data.worlds=[state.data.world];render();const hidden=$('worldLabel').hidden;state.data.worlds=worlds;render();return hidden;});assert(singleWorldHidden,'Single-world selector is still hidden');
  await page.locator('.masthead nav a[href="#world"]').click();
  assert.equal(await page.locator('#onlineCount').textContent(), '2', 'Two live named players');
  assert((await page.locator('#online').textContent()).includes('Astrid Ashwalker'));assert((await page.locator('#mostKilled').textContent()).includes('Greydwarf'),'Most hunted creature types rendered beside stars');const huntRanking=await page.evaluate(()=>{renderMostKilled({creatures:[{prefab:'Troll',name:'Troll',count:3,stars:0},{prefab:'Troll',name:'Troll',count:4,stars:2},{prefab:'Boar',name:'Boar',count:6,stars:0}]});const text=$('mostKilled').textContent;renderMostKilled(state.data.stats);return text;});assert(huntRanking.startsWith('Troll7'),'Creature ranking combines star/biome variants by type');await page.locator('#mostKilled').locator('..').screenshot({path:path.join(output,'most-hunted-types.png')});
  assert.equal(await page.locator('#mapEmpty').isVisible(), false, 'Explored terrain available');
  assert((await page.locator('#online').boundingBox()).y>(await page.locator('#map').boundingBox()).y,'Online roster follows atlas');
  assert((await page.locator('#overview').boundingBox()).y>(await page.locator('#atlas').boundingBox()).y,'Server statistics follow atlas');
  const request = requests.find(r => r.url().includes('/api/state'));
  assert.equal(new URL(request.url()).searchParams.has('players'), false, 'Everyone must omit player restriction');
  assert.equal(request.headers().authorization, 'Bearer ' + token, 'Bearer header authentication');
  assert(!request.url().includes(token), 'Credential never placed in URL');
  await page.screenshot({ path: path.join(output, 'desktop-overview.png'), fullPage: false });
  // First-sync regression: empty exploration must not consume the pending automatic fit.
  await page.evaluate(async()=>{state.busy=true;while(mapStreamBusy)await new Promise(r=>setTimeout(r,10));clearTimeout(mapStreamTimer);});
  let terrainReady=false;
  await page.route('**/api/map-stream?**',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({scope:'synthetic-delayed-map',cursor:'1',reset:true,more:false,cellSize:64,cells:terrainReady?[{x:120,z:-95,biome:'Meadows'}]:[]})}));
  await page.evaluate(async()=>{state.fit=true;await refreshMap();});
  await page.locator('.masthead nav a[href="#world"]').click();
  await page.waitForFunction(()=>!document.querySelector('#atlas').hidden);
  assert.equal(await page.evaluate(()=>state.fit),true,'Empty first map keeps automatic fit pending');
  terrainReady=true;
  await page.evaluate(()=>refreshMap());
  const fitted=await page.evaluate(()=>({point:screen(120.5*64,-94.5*64),width:view.width,height:view.height,fit:state.fit}));
  assert(fitted.point[0]>0&&fitted.point[0]<fitted.width&&fitted.point[1]>0&&fitted.point[1]<fitted.height,'First arriving faraway terrain automatically fitted alongside markers');
  assert.equal(fitted.fit,false,'Successful terrain fit consumed once');
  await page.unroute('**/api/map-stream?**');
  await page.evaluate(async()=>{state.fit=true;await refreshMap();});
  await page.evaluate(()=>state.busy=false);
  await page.locator('.masthead nav a[href="#world"]').click();

  for(const range of ['all','30m','1h','6h','12h','1d','3d','7d']) {
   await revealAtlas(page,'filters');await page.locator('#range').selectOption(range);
   const response=page.waitForResponse(r => r.url().includes('/api/state?')&&new URL(r.url()).searchParams.get('range')===range);
   await revealAtlas(page,'filters');await page.locator('#filters button[type=submit]').click();
   const payload=await (await response).json();
   await page.waitForFunction(n=>document.querySelector('#kills').textContent===n,Number(payload.stats.kills).toLocaleString());
   assert.equal(payload.synthetic,true);
   assert.equal(await page.locator('#onlineCount').textContent(),'2','Presence independent of historical range');
  }
  await revealAtlas(page,'filters');await page.locator('#players').selectOption('fixture-0');
  await revealAtlas(page,'filters');await page.locator('#range').selectOption('all');
  const selected=page.waitForResponse(r=>r.url().includes('/api/state?')&&new URL(r.url()).searchParams.get('players')==='fixture-0');
  await revealAtlas(page,'filters');await page.locator('#filters button[type=submit]').click();
  const selectedData=await (await selected).json();
  assert.equal(selectedData.stats.kills,30,'Single player exact kill count');
  await page.waitForFunction(()=>document.querySelector('#kills').textContent==='30');
  assert.equal(await page.locator('#explorers').inputValue(),'','Independent exploration selection');
  assert.equal(await page.locator('#kills').textContent(),'30','Overview has one coherent filtered total');
  await page.locator('.masthead nav a[href="#world"]').click();
  await revealAtlas(page,'terrain');await page.locator('#linkFilters').check();
  assert.equal(await page.locator('#explorers').inputValue(),'fixture-0','Explicit filter linking');
  await revealAtlas(page,'terrain');await page.locator('#linkFilters').uncheck();
  const union=(await (await page.request.get(url+'api/map',{headers:{Authorization:'Bearer '+token}})).json()).cells.length;
  const personal=(await (await page.request.get(url+'api/map?players=fixture-0',{headers:{Authorization:'Bearer '+token}})).json()).cells.length;
  assert(union>personal,'Combined discovery is union of personal maps');
  await page.locator('#atlas').scrollIntoViewIfNeeded();
  await page.screenshot({path:path.join(output,'desktop-atlas.png')});
  const before=await page.locator('#mapScale').textContent();
  await page.locator('#zoomIn').click();
  assert.notEqual(await page.locator('#mapScale').textContent(),before,'Map zoom changes scale');
  await page.evaluate(()=>zoom(0.000001));const cap=await page.evaluate(()=>({span:Math.min(view.width,view.height)/view.scale,x:view.x,z:view.z}));assert(Math.abs(cap.span-24000)<1,'Zoom-out cap covers a 12 km radius');assert.equal(cap.x,0,'Whole-world zoom centers east/west');assert.equal(cap.z,0,'Whole-world zoom centers north/south');await page.evaluate(()=>{view.x=1e9;view.z=-1e9;drawMap();});assert(await page.evaluate(()=>Math.abs(view.x)+Math.abs(view.z))<1e-6,'Cannot pan the whole world offscreen');await page.setViewportSize({width:900,height:800});await page.evaluate(()=>drawMap());assert(await page.evaluate(()=>Math.min(view.width,view.height)/view.scale<=24000.01),'Resize maintains zoom-out cap');await page.setViewportSize({width:1440,height:1050});
await page.evaluate(()=>drawMap());
  await revealAtlas(page,'vikings');await page.locator('#locatePlayer').selectOption('fixture-0');await revealAtlas(page,'vikings');await page.locator('#locateButton').click();const located=await page.evaluate(()=>({p:screen(-128,0),w:view.width,h:view.height}));assert(Math.abs(located.p[0]-located.w/2)<1&&Math.abs(located.p[1]-located.h/2)<1,'Locate player centers a consented position');await page.locator('#fitMap').click();

  await page.locator('#map').focus();await page.keyboard.press('ArrowRight');
  // Click an actual rendered marker, then follow its profile action.
  const point=await page.evaluate(()=>({x:screen(-128,0)[0],y:screen(-128,0)[1]}));
  await page.locator('#map').click({position:point});
  assert((await page.locator('#mapInspect').textContent()).includes('Astrid Ashwalker'),'Marker opens brief player detail');
  const popupBounds=await page.locator('#mapInspect').boundingBox();assert(popupBounds.width<=300,'Map popup remains tooltip-sized');
  assert.equal(await page.locator('#lootSource').inputValue(),'collect','Loot heat defaults collection only');
  const heatCheck=await page.evaluate(()=>{const rows=heatBuckets([{kind:'kill',x:0,z:0},{kind:'collect',x:0,z:0,amount:7}]);return rows[0].weight;});
  assert.equal(heatCheck,8,'Heat weights actual kill count and item quantity');
  const heatSources=await page.evaluate(()=>{const original=state.data.events;state.data.events=[{kind:'drop',x:0,z:0,amount:7},{kind:'collect',x:0,z:0,amount:7}];$('killLayer').checked=false;$('lootLayer').checked=true;$('lootSource').value='collect';const collected=visibleEvents().map(e=>e.kind);$('lootSource').value='drop';const dropped=visibleEvents().map(e=>e.kind);state.data.events=original;$('killLayer').checked=true;$('lootLayer').checked=false;$('lootSource').value='collect';return {collected,dropped,grouped:inspectEvents([{kind:'collect',name:'Sword',quality:3,rarity:'Rare',rarityColor:'#123456',amount:2},{kind:'collect',name:'Sword',quality:3,rarity:'Rare',rarityColor:'#123456',amount:3}])};});
  assert.deepEqual(heatSources.collected,['collect'],'Collection heat excludes matching drop');assert.deepEqual(heatSources.dropped,['drop'],'Drop heat excludes matching collection');assert(heatSources.grouped.includes('&times;5')&&heatSources.grouped.includes('Quality 3')&&heatSources.grouped.includes('#123456'),'Compact loot popup groups quantity, quality and runtime rarity');

  await revealAtlas(page,'vikings');await page.locator('#onlineMarkers').uncheck();assert.equal(await page.locator('#onlineMarkers').isChecked(),false);await revealAtlas(page,'vikings');await page.locator('#onlineMarkers').check();

  await page.locator('#mapInspect [data-profile]').click();
  await page.waitForFunction(()=>location.hash==='#profile/Astrid-Ashwalker');
  assert.equal(await page.locator('#atlas').isVisible(),false,'Armory routing hides atlas');
  await page.evaluate(()=>location.hash='profile/fixture-1');await page.waitForFunction(()=>document.querySelector('#characterName').textContent==='Bjorn of the Pines');assert.equal(await page.evaluate(()=>state.filter.players.join(',')),'fixture-0','Profile permalink does not alter overview selection');await page.evaluate(()=>location.hash='profile/fixture-0');await page.waitForFunction(()=>document.querySelector('#characterName').textContent==='Astrid Ashwalker');

  const popupUrl=page.url();assert(popupUrl.endsWith('#profile/Astrid-Ashwalker'),'Character has a permalink');
  await page.locator('#characterSearch').fill('Astrid');await page.keyboard.press('ArrowDown');await page.keyboard.press('Enter');
  assert.equal(await page.locator('#characterName').textContent(),'Astrid Ashwalker');
  await page.locator('#characterSearch').fill('Bjorn');await page.keyboard.press('ArrowDown');assert((await page.locator('#characterSearch').getAttribute('aria-activedescendant')).startsWith('viking-option-'),'Search exposes keyboard active suggestion');await page.keyboard.press('Escape');assert.equal(await page.locator('#characterSuggestions').isVisible(),false,'Escape closes Viking suggestions');await page.locator('#characterSearch').fill('Astrid');await page.locator('#characterSuggestions [role="option"]').first().tap();assert.equal(await page.locator('#characterName').textContent(),'Astrid Ashwalker','Touch selects a local Viking suggestion');

  await page.waitForFunction(()=>document.querySelector('#profileMetrics strong').textContent==='30');
  for(const viewport of [{width:1440,height:900},{width:2048,height:1152}]){await page.setViewportSize(viewport);await page.evaluate(()=>scrollTo(0,0));const layout=await page.evaluate(()=>({armory:document.querySelector('#armory').getBoundingClientRect().top,gear:[...document.querySelectorAll('#armory [data-gear]')].map(e=>e.getBoundingClientRect().bottom),career:document.querySelector('#career').getBoundingClientRect().top,loadoutBottom:document.querySelector('#loadout').getBoundingClientRect().bottom}));await page.screenshot({path:path.join(output,'character-first-'+viewport.width+'.png')});assert(layout.armory<460,'Equipment begins above the fold at '+viewport.width);assert(layout.gear.length>0&&layout.gear.every(bottom=>bottom<viewport.height),'Equipped slots visible in first viewport at '+viewport.width);assert(layout.career>layout.loadoutBottom,'Career follows equipment instead of pushing it down');await page.screenshot({path:path.join(output,'character-first-'+viewport.width+'.png')});}
  await page.setViewportSize({width:1440,height:1050});
  await page.locator('[data-profile-tab="saga"]').click();assert.equal(await page.locator('#profileSaga').isVisible(),true,'Saga is its own Viking tab');assert.equal(await page.locator('#profileCharacter').isVisible(),false);await page.locator('[data-profile-tab="character"]').click();assert.equal(await page.locator('#profileCharacter').isVisible(),true);
  const worldRange=await page.locator('#range').inputValue();for(const range of ['30m','1h','6h','12h','1d','3d','7d','all']){await page.locator('#personalRange').selectOption(range);await page.locator('#personalFilters button').click();await page.waitForFunction(r=>state.personalKey===JSON.stringify(personalQuery())&&state.personalFilter.range===r,range);assert.equal(await page.locator('#range').inputValue(),worldRange,'Personal filter does not change World filter');}
  await page.locator('#personalRange').selectOption('custom');await page.locator('#personalFrom').fill('2020-01-01T00:00');await page.locator('#personalTo').fill('2030-01-01T00:00');await page.locator('#personalFilters button').click();await page.waitForFunction(()=>state.personalFilter.range==='custom'&&state.personalKey===JSON.stringify(personalQuery()));assert.equal(await page.locator('#profileMetrics strong').first().textContent(),'30','Personal custom dates use character-specific stats');await page.locator('#personalRange').selectOption('all');await page.locator('#personalFilters button').click();await page.waitForFunction(()=>state.personalFilter.range==='all'&&state.personalKey===JSON.stringify(personalQuery()));

  await page.evaluate(async()=>{state.filter.range='30m';await refreshProfile();});assert.equal(await page.locator('#profileMetrics strong').first().textContent(),'30','Character lifetime totals ignore overview recent range');assert.equal(await page.evaluate(()=>profileQuery().range),'all');await page.evaluate(()=>state.filter.range='all');
  const semantic=await page.evaluate(()=>equipmentPlacement([{type:'Shield',slot:'LeftHand'},{type:'Bow',slot:'RightHand'},{type:'Ammo',slot:'Ammo'},{type:'Helmet',slot:'Head'},{type:'OneHandedWeapon',slot:'RightHand'}]).map(v=>({type:v.g.type,column:v.column,row:v.row})));
  assert(semantic.filter(v=>/Shield|Ammo/.test(v.type)).every(v=>v.column===3&&v.row>=5),'Shield and ammo lower right');assert(semantic.filter(v=>/Bow|Weapon/.test(v.type)).every(v=>v.column===1&&v.row>=5),'Weapons and bows lower left');
  const about=await page.evaluate(()=>{state.profileData.chapters=[{playerId:state.profile,utc:new Date().toISOString(),promptVersion:'sagas-3',characterBio:'Synthetic biography inspired by the recorded hunt.',characterBioModel:'openrouter/free',summary:'Legacy summary fallback.',text:'Synthetic chapter.',model:'openrouter/free',facts:['Recorded fact: defeated one synthetic creature.']}];renderProfile();return {bio:$('characterBio').textContent,source:$('characterBioSource').textContent,facts:$('characterFacts').textContent};});assert(about.bio.includes('Synthetic biography'),'Biography uses persistent server chapter summary');assert(about.source.includes('AI lore'),'Biography labels AI fiction');assert(about.facts.includes('Recorded fact:'),'Biography retains separate fact ledger');const legacyBio=await page.evaluate(()=>{state.profileData.chapters=[{playerId:state.profile,utc:new Date().toISOString(),text:'Synthetic legacy narrative around the hearth.',summary:'Recorded kills: 42',model:'local-template',facts:[]}];renderProfile();return $('characterBio').textContent;});assert(legacyBio.includes('No AI biography yet')&&!legacyBio.includes('42'),'Legacy template is hidden without inventing a fallback biography');await page.evaluate(()=>refreshProfile());

  await page.locator('[data-gear="0"]').focus();
  assert.equal(await page.locator('#tooltip').isVisible(),true,'Keyboard tooltip');
  assert((await page.locator('#tooltip').textContent()).includes('Base 20'),'Base item value');
  assert((await page.locator('#tooltip').textContent()).includes('quality contribution 4'),'Quality contribution');
  await page.keyboard.press('Escape');
  await page.locator('[data-gear="1"]').hover();
  assert((await page.locator('#tooltip').textContent()).includes('Silver sword'),'Mouse tooltip');
  await page.screenshot({path:path.join(output,'desktop-armory.png')});
  await page.keyboard.press('Escape');
  await page.locator('.masthead nav a[href="#world"]').click();
  await revealAtlas(page,'filters');await page.locator('#range').selectOption('custom');
  await revealAtlas(page,'filters');await page.locator('#from').fill('2026-01-02T10:00');
  await revealAtlas(page,'filters');await page.locator('#to').fill('2026-01-01T10:00');
  await revealAtlas(page,'filters');await page.locator('#filters button[type=submit]').click();
  assert((await page.locator('#connection').textContent()).includes('valid custom'),'Reject inverted dates');
  await revealAtlas(page,'filters');await page.locator('#from').fill('2020-01-01T00:00');
  await revealAtlas(page,'filters');await page.locator('#to').fill('2030-01-01T00:00');
  const custom=page.waitForResponse(r=>r.url().includes('range=custom'));
  await revealAtlas(page,'filters');await page.locator('#filters button[type=submit]').click();
  assert.equal((await custom).status(),200,'Custom UTC-converted date request');
  // Escaping validation uses synthetic response interception, deliberately distinct from live fixture checks.
  await page.route('**/api/state?**',async route=>{
   const response=await route.fetch();const json=await response.json();
   json.players[0].name='<img src=x onerror="window.sagasInjected=true">';
   json.eventsTruncated=true;json.eventCount=9000;json.server={name:'Synthetic longhouse',address:'192.0.2.10:2456'};
   await route.fulfill({response,json});
  });
  await revealAtlas(page,'filters');await page.locator('#filters button[type=submit]').click();
  await page.waitForFunction(()=>document.querySelector('#history').textContent.includes('5,000'));
  await page.waitForFunction(()=>document.querySelector('#serverName').textContent==='Synthetic longhouse');
  assert((await page.locator('#serverAddress').textContent()).includes('192.0.2.10:2456'),'Configured server address displayed');
  assert.equal(await page.evaluate(()=>window.sagasInjected),undefined,'Telemetry cannot execute markup');
  assert.equal(await page.locator('#online img').count(),0,'Names rendered as escaped text');
  await page.unrouteAll({behavior:'wait'});
  await revealAtlas(page,'filters');await page.locator('#range').selectOption('all');
  const restored=page.waitForResponse(r=>r.url().includes('/api/state?')&&new URL(r.url()).searchParams.get('range')==='all');
  await revealAtlas(page,'filters');await page.locator('#filters button[type=submit]').click();
  await restored;
  await page.waitForFunction(()=>document.querySelector('#characterName').textContent==='Astrid Ashwalker');
  await page.setViewportSize({width:390,height:844});
  await page.locator('#overview').scrollIntoViewIfNeeded();
  await page.screenshot({path:path.join(output,'mobile-overflow-debug.png'),fullPage:true});
  const overflow=await page.evaluate(()=>[...document.querySelectorAll('body *')].filter(e=>{const b=e.getBoundingClientRect();return b.right>innerWidth+1||b.left<0}).map(e=>[e.tagName,e.id,e.className,e.getBoundingClientRect().width,e.getBoundingClientRect().right]).slice(0,30));
  if(overflow.length)console.log(overflow);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,'Mobile has no page overflow');
  await page.screenshot({path:path.join(output,'mobile-overview.png')});
  await page.locator('.masthead nav a[href="#profile"]').click();await page.locator('#vikingDirectorySearch').fill('Astrid');await page.locator('#vikingDirectoryResults [data-profile="fixture-0"]').click();
  await page.locator('#profile').scrollIntoViewIfNeeded();
  await page.locator('[data-gear="0"]').tap();
  assert.equal(await page.locator('#tooltip').isVisible(),true,'Touch tap tooltip');
  const box=await page.locator('#tooltip').boundingBox();
  assert(box.x>=0&&box.x+box.width<=391,'Tooltip stays inside mobile viewport');
  await page.screenshot({path:path.join(output,'mobile-armory.png')});
  // Synthetic adapter contracts: runtime color, north-up alpha terrain, private media.
  const tileResult=await page.evaluate(()=>{const bytes=new Uint8Array(1024);bytes[0]=255;bytes[3]=255;const tile=terrainTile({terrainPixels:btoa(String.fromCharCode(...bytes))});return {south:[...tile.getContext('2d').getImageData(0,15,1,1).data],north:[...tile.getContext('2d').getImageData(0,0,1,1).data],safe:rarityColor('Rare','url(https://bad)'),custom:rarityColor('Rare','#123456'),highRes:terrainTile({terrainPixels:btoa(String.fromCharCode(...new Uint8Array(64*64*4)))}).width};});
  assert.deepEqual(tileResult.south,[255,0,0,255],'South-first pixels rendered at bottom');
  assert.deepEqual(tileResult.north,[0,0,0,0],'Unexplored terrain remains transparent');
  assert.equal(tileResult.highRes,64,'Higher-resolution 64x64 tiles supported');
  assert.equal(tileResult.custom,'#123456','Epic Loot supplied color wins');
  assert.notEqual(tileResult.safe,'url(https://bad)','Untrusted CSS rejected');
  const pixel=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=','base64');
  let mediaRequest;
  await page.route('**/api/media/**',route=>{mediaRequest=route.request();return route.fulfill({status:200,contentType:'image/png',body:pixel});});
  await page.evaluate(()=>{const p=state.data.players.find(p=>p.playerId===state.profile);p.portraitId='synthetic-portrait';p.portraitStatus='simplified';p.gear[0].iconId='synthetic-item';p.gear[0].rarityColor='#123456';p.effectiveResistances={Frost:'Resistant'};renderProfile();});
  await page.waitForFunction(()=>document.querySelector('.armory-symbol img')?.naturalWidth>0&&document.querySelector('.item-icon img')?.naturalWidth>0);
  assert.equal(await page.locator('#portraitStatus').textContent(),'Simplified character preview','Simplified portrait remains labelled after image loads');
  assert.equal(await page.locator('#portraitStatus').isVisible(),true,'Simplified label remains visible');
  assert.equal(mediaRequest.headers().authorization,'Bearer '+token,'Private media uses bearer header');
  assert(!mediaRequest.url().includes(token),'Image URLs contain no credential');
  assert((await page.locator('#effectiveStats').textContent()).includes('Frost resistanceResistant'),'Effective resistance displayed');
  await page.unroute('**/api/media/**');
  await page.evaluate(()=>{const p=state.data.players.find(p=>p.playerId===state.profile);p.hotbar=[{name:'Utility belt',type:'Utility',slot:'utility',quality:1,hotbarSlot:3,equipped:true,active:true}];renderProfile();});
  assert.equal(await page.locator('#hotbar .hotbar-slot').count(),8,'All eight numbered hotbar slots are present');
  assert.equal(await page.locator('#hotbar .hotbar-slot').nth(0).getAttribute('aria-label'),'Hotbar 1: empty','Empty inventory slot retained');
  assert((await page.locator('#hotbar .hotbar-slot').nth(2).textContent()).includes('Utility belt'),'Item keeps its actual first-row slot');
  await page.locator('#hotbar [data-hotbar]').focus();
  const itemLabel=await page.locator('#tooltip p').first().textContent();assert.equal((itemLabel.toLowerCase().match(/utility/g)||[]).length,1,'Item type and slot label deduplicated case-insensitively');
  const portraitBounds=await page.locator('.armory-symbol').boundingBox();assert(portraitBounds.width>=100&&portraitBounds.height>=300,'Center portrait remains legible on mobile');
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'Hotbar and portrait do not overflow mobile');
  await page.keyboard.press('Escape');await page.locator('#armory').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(output,'mobile-armory-hotbar.png')});
  const captureStatus=await page.evaluate(()=>{const p=state.data.players.find(p=>p.playerId===state.profile);p.portraitId='';p.portraitStatus='Waiting for the next character capture';renderProfile();return $('portraitStatus').textContent;});
  assert(captureStatus.includes('next character capture'),'Portrait capture status is explicit');
  await page.route('**/api/media/**',route=>route.fulfill({status:200,contentType:'text/plain',body:'Synthetic unavailable image'}));await page.evaluate(()=>{const p=state.data.players.find(p=>p.playerId===state.profile);p.portraitId='synthetic-failed-portrait';renderProfile();});await page.waitForFunction(()=>document.querySelector('#portraitStatus').textContent.includes('Portrait unavailable'));assert.equal(await page.locator('#portraitStatus').isVisible(),true,'Portrait load failure is visible');await page.unroute('**/api/media/**');


  assert.deepEqual(errors,[],'No page errors or CSP violations');
  // Real backend leaderboard from a separate synthetic world; no intercepted ranking values.
  await page.locator('#world').selectOption('synthetic-leaderboard');await page.waitForFunction(()=>state.data?.world==='synthetic-leaderboard');await page.locator('.masthead nav a[href="#leaderboard"]').click();await page.waitForFunction(()=>document.querySelector('#leaderboardResults').textContent.includes('Synthetic Sea Guardian'));const ranking=await (await page.request.get(url+'api/leaderboard?world=synthetic-leaderboard&range=all',{headers:{Authorization:'Bearer '+token}})).json();assert.equal(ranking.bosses.find(b=>b.key==='Eikthyr').uniquePlayers,3,'Two boss encounters preserve three distinct contributors');assert.equal(ranking.bosses.find(b=>b.key==='Bonemass').fastest,null,'Legacy boss has no fabricated duration');assert.equal(ranking.bosses.find(b=>b.key==='SyntheticSeaGuardian').order,null,'Modded boss receives no invented progression tier');assert.equal(ranking.fastestBossKills[0].durationSeconds,75,'Actual measured boss duration ranks correctly');assert.equal(ranking.bounties[0].value,2,'Recorded bounty transitions rank without historical import');assert.equal(ranking.gold[0].value,1250,'Latest carried gold snapshot ranks');assert(!ranking.gold.some(p=>p.playerId==='trial-private'),'Private gold snapshot excluded from leaderboard');assert.equal((await page.request.get(url+'api/leaderboard?world=synthetic-leaderboard')).status(),401,'Private leaderboard requires member authentication');assert((await page.locator('#leaderboardResults').textContent()).includes('Not measured'),'Untimed legacy encounter labelled unavailable');assert((await page.locator('#leaderboardResults').textContent()).includes('Finisher: Bjorn Shieldbearer'),'Team and finisher explicitly distinguished');await page.locator('#fastestBoss').selectOption('Eikthyr');assert.equal(await page.locator('#fastestBoss').inputValue(),'Eikthyr','Measured fights can compare one boss');assert((await page.locator('#fastestBoss').locator('..').locator('..').textContent()).includes('Stars'),'Measured comparisons show star count');
  for(const range of ['30m','1h','6h','12h','1d','3d','7d','all']){await page.locator('#leaderboardRange').selectOption(range);const next=page.waitForResponse(r=>r.url().includes('/api/leaderboard?')&&new URL(r.url()).searchParams.get('range')===range);await page.locator('#leaderboardFilters button').click();assert.equal((await next).status(),200,'Leaderboard supports '+range);}
  await page.locator('#leaderboardRange').selectOption('custom');await page.locator('#leaderboardFrom').fill('2020-01-01T00:00');await page.locator('#leaderboardTo').fill('2030-01-01T00:00');const customBoard=page.waitForResponse(r=>r.url().includes('/api/leaderboard?')&&r.url().includes('range=custom'));await page.locator('#leaderboardFilters button').click();assert.equal((await customBoard).status(),200,'Leaderboard custom dates');await page.setViewportSize({width:1440,height:1050});await page.evaluate(()=>scrollTo({top:0,behavior:'instant'}));await page.screenshot({path:path.join(output,'leaderboard-desktop.png'),fullPage:true});await page.setViewportSize({width:390,height:844});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'Leaderboard fits mobile viewport');await page.screenshot({path:path.join(output,'leaderboard-mobile.png'),fullPage:true});
  await page.route('**/api/leaderboard?**',async route=>{const response=await route.fetch();const json=await response.json();json.highestStars[0].name='<img src=x onerror="window.boardInjected=true">';await route.fulfill({response,json});});await page.evaluate(()=>refreshLeaderboard());assert.equal(await page.locator('#leaderboardResults img').count(),0,'Leaderboard escapes player markup');assert.equal(await page.evaluate(()=>window.boardInjected),undefined,'Leaderboard telemetry cannot execute markup');await page.unroute('**/api/leaderboard?**');
  await page.locator('#world').selectOption('synthetic-midgard');await page.waitForFunction(()=>state.data?.world==='synthetic-midgard');await page.locator('#leaderboardRange').selectOption('all');await page.locator('#leaderboardFilters button').click();await page.waitForFunction(()=>document.querySelector('#leaderboardResults').textContent.includes('No gold snapshots'));assert((await page.locator('#leaderboardResults').textContent()).includes('No measured boss fights'),'Missing durations not ranked as zero');await page.locator('.masthead nav a[href="#profile"]').click();await page.locator('#vikingDirectorySearch').fill('Astrid');await page.locator('#vikingDirectoryResults [data-profile="fixture-0"]').click();await page.locator('#characterSearch').fill('Astrid');await page.keyboard.press('ArrowDown');await page.keyboard.press('Enter');

  let realSaga;for(let attempt=0;attempt<30;attempt++){realSaga=await (await page.request.get(url+'api/server-saga?world=synthetic-midgard',{headers:{Authorization:'Bearer '+token}})).json();if(realSaga.chapters?.length)break;await new Promise(resolve=>setTimeout(resolve,1000));}assert(realSaga.chapters?.length>0,'Actual fixture server queue has persisted a local server chapter');await page.locator('.masthead nav a[href="#server-saga"]').click();await page.waitForFunction(title=>document.querySelector('#serverSagaChapters').textContent.includes(title),realSaga.chapters[0].title);assert((await page.locator('#serverSagaChapters').textContent()).includes(realSaga.chapters[0].facts[0]),'Real persisted server chapter and facts reach the browser');await page.screenshot({path:path.join(output,'server-saga-real-local.png')});
  await page.route('**/api/server-saga?**',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({world:'synthetic-midgard',synthetic:true,bio:'',chapters:[],pending:false})}));await page.locator('.masthead nav a[href="#server-saga"]').click();await page.waitForFunction(()=>document.querySelector('#serverSagaStatus').textContent.includes('OpenRouter'));assert.equal(await page.locator('#serverSagaChapters .chapter').count(),0,'Empty server saga never fabricates chapters');await page.unroute('**/api/server-saga?**');
  await page.route('**/api/server-saga?**',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({world:'synthetic-midgard',synthetic:true,bio:'Synthetic fellowship introduction.',bioModel:'fixture/generated',chapters:[{title:'Synthetic recorded chapter',text:'Synthetic server narrative.',model:'fixture/generated',utc:new Date().toISOString(),facts:['Recorded synthetic encounter.'],participants:[{playerId:'fixture-0',name:'Astrid Ashwalker'}]}],pending:false})}));await page.evaluate(()=>refreshServerSaga());await page.waitForFunction(()=>document.querySelector('#serverSagaChapters').textContent.includes('Synthetic recorded chapter'));assert((await page.locator('#serverSagaChapters').textContent()).includes('Recorded synthetic encounter.'),'Server saga separates persisted prose and facts');assert((await page.locator('#serverSagaChapters').textContent()).includes('Astrid Ashwalker'),'Server saga includes consented participants');assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'Server saga fits mobile viewport');await page.screenshot({path:path.join(output,'server-saga.png')});await page.unroute('**/api/server-saga?**');
  await page.locator('.masthead nav a[href="#profile"]').click();await page.locator('#vikingDirectorySearch').fill('Astrid');await page.locator('#vikingDirectoryResults [data-profile="fixture-0"]').click();await page.locator('[data-profile-tab="saga"]').click();await page.screenshot({path:path.join(output,'viking-saga.png')});await page.locator('[data-profile-tab="character"]').click();

  await page.locator('.masthead nav a[href="#world"]').click();
  await page.route('**/api/state?**',async route=>{
   const response=await route.fetch();const json=await response.json();
   json.health={storageError:'SyntheticStorageFailure',errorId:'fixture-health-011'};
   await route.fulfill({response,json});
  });
  await revealAtlas(page,'filters');await page.locator('#filters button[type=submit]').click();
  await page.waitForFunction(()=>document.querySelector('#connection').textContent.includes('fixture-health-011'));
  assert(!(await page.locator('#connection').textContent()).includes('Connected'),'Unhealthy collector cannot claim connected success');
  await page.unrouteAll({behavior:'wait'});
  // Deliberate synthetic server failure must remain visible with a support reference.
  await page.route('**/api/state?**', route => route.fulfill({status:500,contentType:'application/json',body:JSON.stringify({error:'Recorded data is temporarily unavailable',errorId:'fixture-error-011'})}));
  await revealAtlas(page,'filters');await page.locator('#filters button[type=submit]').click();
  await page.waitForFunction(()=>document.querySelector('#connection').textContent.includes('fixture-error-011'));
  assert((await page.locator('#connection').textContent()).includes('500'),'HTTP status visible');
  assert((await page.locator('#connection').textContent()).includes('last known'),'Stale data explicitly labelled');
  await page.unrouteAll({behavior:'wait'});
  await revealAtlas(page,'filters');await page.locator('#filters button[type=submit]').click();
  await page.waitForFunction(()=>document.querySelector('#connection').textContent.includes('Connected'));
  // Isolated response adapter exercises public browser behavior against the same synthetic data.
  const publicPage=await browser.newPage();const publicRequests=[];
  publicPage.on('request',r=>{if(r.url().includes('/api/'))publicRequests.push(r);});
  await publicPage.route('**/api/**',async route=>{if(route.request().url().includes('/api/media/'))return route.fulfill({status:200,contentType:'image/png',body:pixel});if(route.request().url().endsWith('/api/access'))return route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({requiresToken:false})});const response=await route.fetch({headers:{...route.request().headers(),Authorization:'Bearer '+token}});if(route.request().url().includes('/api/state')){const json=await response.json();json.players.find(p=>p.playerId==='fixture-0').portraitId='synthetic-public-portrait';return route.fulfill({response,json});}await route.fulfill({response});});
  await publicPage.goto(url+'#profile/fixture-0');await publicPage.waitForFunction(()=>document.querySelector('#connection').textContent.includes('Connected'));
  assert.equal(await publicPage.locator('#access').isVisible(),false,'Public viewing never opens token dialog');
  assert.equal(await publicPage.locator('#accessButton').textContent(),'Login','Public access permits optional player login');
  assert.equal(await publicPage.locator('#characterName').textContent(),'Astrid Ashwalker','Public profile permalink loads without token');
  await publicPage.locator('#armory').scrollIntoViewIfNeeded();await publicPage.waitForFunction(()=>document.querySelector('.armory-symbol img')?.naturalWidth>0);assert(publicRequests.filter(r=>r.url().includes('/api/media/')).every(r=>!r.headers().authorization),'Public media loads without token');

  assert(publicRequests.filter(r=>r.url().includes('/api/state')).every(r=>!r.headers().authorization),'Public browser sends no bearer credential');
  assert.equal(await publicPage.evaluate(()=>sessionStorage.getItem('sagas.viewer')),null,'Public viewing does not store credentials');
  await publicPage.evaluate(()=>location.hash='atlas');await publicPage.reload();await publicPage.waitForFunction(()=>!state.fit&&state.map.cells.length>0&&state.data?.players?.length>0);const refreshedMarkers=await publicPage.evaluate(()=>visibleMarkers().map(p=>({point:screen(p.x,p.z),width:view.width,height:view.height})));assert(refreshedMarkers.length>0&&refreshedMarkers.every(m=>m.point[0]>=0&&m.point[0]<=m.width&&m.point[1]>=0&&m.point[1]<=m.height),'Markers stay within initial fit after browser reload');await revealAtlas(publicPage,'vikings');await publicPage.locator('#offlineMarkers').check();await publicPage.evaluate(()=>refresh());assert.equal(await publicPage.locator('#offlineMarkers').isChecked(),true,'Polling preserves marker layer controls');await publicPage.locator('.masthead nav a[href="#server-saga"]').click();await publicPage.waitForFunction(()=>document.querySelector('#serverSagaChapters .chapter'));assert(publicRequests.filter(r=>r.url().includes('/api/server-saga')).every(r=>!r.headers().authorization),'Public server saga requires no browser credential');await publicPage.locator('.masthead nav a[href="#leaderboard"]').click();await publicPage.waitForFunction(()=>document.querySelector('#leaderboardResults').textContent.includes('Creature kills'));assert(publicRequests.filter(r=>r.url().includes('/api/leaderboard')).every(r=>!r.headers().authorization),'Public leaderboard uses no browser credential');
await publicPage.route('**/api/state?**',route=>route.fulfill({status:401,contentType:'application/json',body:JSON.stringify({error:'Token now required'})}));await publicPage.evaluate(()=>refresh());await publicPage.waitForFunction(()=>document.querySelector('#accessButton').textContent==='Login');assert.equal(await publicPage.locator('#accessButton').isEnabled(),true,'Public-to-private change restores login button');assert.equal(await publicPage.evaluate(()=>state.publicViewing),false,'401 stops unauthenticated public polling');await publicPage.unrouteAll({behavior:'wait'});await publicPage.close();
  // New browser session sees retained offline equipment; loaded art survives a host outage in memory.
  const offlinePage=await browser.newPage({viewport:{width:1440,height:900},hasTouch:true});await offlinePage.addInitScript(token=>sessionStorage.setItem('sagas.viewer',token),token);const offlineMedia=[];
  await offlinePage.route('**/api/state?**',async route=>{const response=await route.fetch();const json=await response.json();for(const p of json.players){p.online=false;p.portraitId='shared-synthetic-art';p.gear[0].iconId='shared-synthetic-art';p.portraitStatus='ready';}await route.fulfill({response,json});});
  await offlinePage.route('**/api/media/**',async route=>{offlineMedia.push(route.request().url());await new Promise(resolve=>setTimeout(resolve,new URL(route.request().url()).searchParams.get('player')==='fixture-0'?150:20));await route.fulfill({status:200,contentType:'image/png',body:pixel});});
  await offlinePage.goto(url+'#world');await offlinePage.waitForFunction(()=>state.data?.players?.some(p=>!p.online));assert.equal(await offlinePage.locator('.masthead #characterSearch').isVisible(),true,'Viking search is available from World banner');await offlinePage.locator('#characterSearch').fill('Astrid');assert((await offlinePage.locator('#characterSuggestions').textContent()).includes('Offline'),'Global search includes offline Vikings');await offlinePage.screenshot({path:path.join(output,'shared-search-desktop.png')});await offlinePage.keyboard.press('ArrowDown');await offlinePage.keyboard.press('Enter');await offlinePage.waitForFunction(()=>location.hash==='#profile/Astrid-Ashwalker');await offlinePage.waitForFunction(()=>document.querySelector('#characterStatus').textContent.includes('Offline'));await offlinePage.locator('#armory').scrollIntoViewIfNeeded();await offlinePage.waitForFunction(()=>document.querySelector('#armory .item-icon img')?.naturalWidth>0&&document.querySelector('#characterAvatar img')?.naturalWidth>0);assert.equal(await offlinePage.locator('#characterAvatar').evaluate(e=>getComputedStyle(e).borderRadius),'50%','Header uses circular actual portrait');
  await offlinePage.locator('#characterSearch').fill('Bjorn');assert((await offlinePage.locator('#characterSuggestions').textContent()).includes('Offline'),'Offline Vikings remain searchable');await offlinePage.keyboard.press('ArrowDown');await offlinePage.keyboard.press('Enter');await offlinePage.waitForFunction(()=>state.profile==='fixture-1'&&document.querySelector('#characterAvatar img')?.src===mediaCache.get(state.filter.world+'|fixture-1|shared-synthetic-art|head'));assert(offlineMedia.some(url=>new URL(url).searchParams.get('player')==='fixture-1'),'Same asset ID still respects owner authorization/cache binding');
  await offlinePage.evaluate(async()=>{state.busy=true;for(const p of state.data.players)p.portraitId='racing-art';state.profile='fixture-0';renderProfile();state.profile='fixture-1';renderProfile();await loadMedia('fixture-1');await new Promise(resolve=>setTimeout(resolve,200));});assert.equal(await offlinePage.evaluate(()=>document.querySelector('#characterAvatar img').src===mediaCache.get(state.filter.world+'|fixture-1|racing-art|head')),true,'Late previous-owner response cannot replace current portrait');await offlinePage.evaluate(()=>state.busy=false);
  await offlinePage.waitForLoadState('networkidle');await offlinePage.context().setOffline(true);const cached=await offlinePage.evaluate(async()=>{const p=state.data.players.find(p=>p.playerId===state.profile);p.gear[0].name+=' (last known)';renderProfile();await loadMedia(p.playerId);return document.querySelector('#armory .item-icon img').src;});await offlinePage.waitForFunction(()=>document.querySelector('#armory .item-icon img')?.naturalWidth>0);assert(cached.startsWith('blob:'),'Cached offline equipment stays visible after DOM rebuild');assert.equal(await offlinePage.locator('#armory .item-icon img').evaluate(e=>getComputedStyle(e).display!=='none'),true,'Network loss does not hide retained art');
  await offlinePage.context().setOffline(false);const countBeforeReload=offlineMedia.length;await offlinePage.waitForLoadState('networkidle');await offlinePage.reload();await offlinePage.waitForFunction(()=>document.querySelector('#characterStatus').textContent.includes('Offline'));await offlinePage.locator('#armory').scrollIntoViewIfNeeded();await offlinePage.waitForFunction(()=>document.querySelector('#armory .item-icon img')?.naturalWidth>0);assert(offlineMedia.length>countBeforeReload,'Fresh session reload fetches retained offline art from server');await offlinePage.evaluate(()=>scrollTo({top:0,behavior:'instant'}));await offlinePage.screenshot({path:path.join(output,'offline-viking-retained-art.png')});await offlinePage.setViewportSize({width:390,height:844});await offlinePage.locator('.masthead nav a[href="#world"]').click();await offlinePage.locator('#characterSearch').fill('Bjorn');assert.equal(await offlinePage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'Shared search fits mobile banner');await offlinePage.screenshot({path:path.join(output,'shared-search-mobile.png')});await offlinePage.locator('#characterSuggestions [role="option"]').first().tap();await offlinePage.waitForFunction(()=>location.hash==='#profile/Bjorn-of-the-Pines');await offlinePage.evaluate(()=>state.busy=true);await offlinePage.unrouteAll({behavior:'ignoreErrors'});await offlinePage.close();
  // Explicitly synthetic padded character: tan head, blue torso, red boots. No copied game art.
  const portraitPage=await browser.newPage({viewport:{width:1440,height:900}});const portraitErrors=[];portraitPage.on('pageerror',e=>portraitErrors.push(e.message));portraitPage.on('response',r=>{if(r.url().includes('/biomes/')&&r.status()>=400)portraitErrors.push('Artwork HTTP '+r.status()+': '+r.url());});await portraitPage.addInitScript(token=>sessionStorage.setItem('sagas.viewer',token),token);await portraitPage.goto(url+'#profile/fixture-0');await portraitPage.waitForFunction(()=>state.profileData?.stats&&!state.busy);await portraitPage.waitForLoadState('networkidle');const crop=await portraitPage.evaluate(async()=>{state.busy=true;const canvas=document.createElement('canvas');canvas.width=400;canvas.height=600;const ctx=canvas.getContext('2d');ctx.fillStyle='#e0b080';ctx.fillRect(183,185,34,34);ctx.fillStyle='#326aa0';ctx.fillRect(170,219,60,90);ctx.fillStyle='#b02020';ctx.fillRect(176,309,18,51);ctx.fillRect(206,309,18,51);const blob=await new Promise(resolve=>canvas.toBlob(resolve));const p=state.data.players.find(p=>p.playerId===state.profile);p.portraitId='explicit-synthetic-padded-body';p.portraitStatus='ready';mediaCache.set(state.filter.world+'|'+p.playerId+'|'+p.portraitId,URL.createObjectURL(blob));renderProfile();await loadMedia(p.playerId);await Promise.all([...document.querySelectorAll('#characterAvatar img,.armory-symbol img')].map(img=>img.decode()));const body=document.querySelector('.armory-symbol img'),head=document.querySelector('#characterAvatar img');const check=document.createElement('canvas');check.width=head.naturalWidth;check.height=head.naturalHeight;const c=check.getContext('2d');c.drawImage(head,0,0);const bytes=c.getImageData(0,0,check.width,check.height).data;let red=0,tan=0;for(let i=0;i<bytes.length;i+=4){if(bytes[i]>150&&bytes[i+1]<70&&bytes[i+3]>100)red++;if(bytes[i]>180&&bytes[i+1]>120&&bytes[i+3]>100)tan++;}return {width:body.naturalWidth,height:body.naturalHeight,red,tan,initials:getComputedStyle(document.querySelector('#characterAvatar>span')).visibility};});assert(crop.height<200&&crop.height>170&&crop.width<80,'Transparent borders are cropped from old saved body art');assert(crop.tan>0&&crop.red===0,'Avatar contains actual head, excludes synthetic red boots');assert.equal(crop.initials,'hidden','Successful head portrait removes lettering');assert.equal(await portraitPage.locator('#loadout h2').count(),0,'No redundant Equipment header');await portraitPage.screenshot({path:path.join(output,'portrait-cropped-themed-desktop.png')});
  const theme=await portraitPage.evaluate(()=>({key:document.querySelector('#loadout').dataset.biome,evidence:document.querySelector('#biomeEvidence').textContent}));assert.equal(theme.key,'meadows');assert(theme.evidence.includes('Ambient'));for(const range of ['all','30m']){const r=await portraitPage.request.get(url+'api/state?world=synthetic-leaderboard&range='+range,{headers:{Authorization:'Bearer '+token}});const data=await r.json();assert.equal(data.players.find(p=>p.playerId==='trial-0').profileBiome,'Mountain','Retained boss progression unaffected by time window');assert.equal(data.players.find(p=>p.playerId==='trial-private').profileBiome,'','Private profile theme withheld');}await portraitPage.evaluate(()=>{const p=state.data.players.find(p=>p.playerId===state.profile);p.profileBiome='Swamp';p.profileBiomeEvidence='Synthetic recorded Bonemass victory';renderProfile();});assert.equal(await portraitPage.locator('#loadout').getAttribute('data-biome'),'swamp');await portraitPage.setViewportSize({width:390,height:844});await portraitPage.screenshot({path:path.join(output,'portrait-cropped-themed-mobile.png')});assert.equal(await portraitPage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  assert.equal(await portraitPage.locator('[data-backdrop="scroll"]').getAttribute('aria-pressed'),'true','Full screen scroll is the initial default');
  await portraitPage.locator('#appearance').evaluate(e=>e.open=true);await portraitPage.locator('[data-backdrop="full"]').click();
  assert.equal(await portraitPage.evaluate(()=>document.body.classList.contains('scenic-profile')),true,'Full screen removes the box and expands scenery');
  assert.equal(await portraitPage.locator('#loadout').evaluate(e=>getComputedStyle(e).backgroundImage),'none','Full screen has no duplicate boxed background');
  assert.equal(await portraitPage.evaluate(()=>localStorage.getItem('sagas.backdrop')),'full','Only presentation choice is saved');
  await portraitPage.screenshot({path:path.join(output,'backdrop-full-mobile.png')});
  for(const size of [{width:1440,height:1000},{width:2560,height:1080}]){await portraitPage.setViewportSize(size);await portraitPage.mouse.move(0,0);await portraitPage.evaluate(()=>hideTip());assert.equal(await portraitPage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'Scenic background fits viewport without overflow');await portraitPage.screenshot({path:path.join(output,'backdrop-full-'+size.width+'.png')});}
  await portraitPage.locator('[data-profile-tab="saga"]').click();assert.equal(await portraitPage.evaluate(()=>document.body.classList.contains('scenic-profile')),true,'Full screen scenery remains on Saga tab');assert.equal(await portraitPage.locator('#appearance summary').isVisible(),true,'Saga keeps backdrop controls available');await portraitPage.screenshot({path:path.join(output,'backdrop-full-saga.png')});
  await portraitPage.locator('[data-profile-tab="character"]').click();assert.equal(await portraitPage.evaluate(()=>document.body.classList.contains('scenic-profile')),true,'Character restores chosen scenery');
  await portraitPage.locator('.masthead nav a[href="#world"]').click();await portraitPage.waitForFunction(()=>document.body.dataset.page==='world');assert.equal(await portraitPage.evaluate(()=>document.body.classList.contains('scenic-profile')),false,'World route does not inherit character scenery');
  await portraitPage.locator('.masthead nav a[href="#profile"]').click();await portraitPage.locator('#vikingDirectorySearch').fill('Astrid');await portraitPage.locator('#vikingDirectoryResults [data-profile="fixture-0"]').click();
  await portraitPage.evaluate(()=>{const p=state.data.players.find(p=>p.playerId===state.profile);p.profileBiome='';renderProfile();});assert.equal(await portraitPage.evaluate(()=>document.body.classList.contains('scenic-profile')),false,'Withheld profile theme cannot leave previous scenery visible');
  await portraitPage.evaluate(()=>{state.profile='unavailable-viking';renderProfile();});assert.equal(await portraitPage.evaluate(()=>document.body.style.getPropertyValue('--profile-backdrop')),'none','Missing profile clears backdrop');
  await portraitPage.goto(url+'#profile/fixture-0');await portraitPage.reload();await portraitPage.waitForFunction(()=>state.profileData?.stats&&!state.busy);assert.equal(await portraitPage.locator('[data-backdrop="full"]').getAttribute('aria-pressed'),'true','Choice survives reload');
  const highResolution=await portraitPage.evaluate(async()=>{state.busy=true;const c=document.createElement('canvas');c.width=768;c.height=1152;const ctx=c.getContext('2d');ctx.fillStyle='#e0b080';ctx.fillRect(304,80,160,160);ctx.fillStyle='#326aa0';ctx.fillRect(230,240,308,540);ctx.fillStyle='#b02020';ctx.fillRect(255,780,100,260);ctx.fillRect(410,780,100,260);const blob=await new Promise(r=>c.toBlob(r));const p=state.data.players.find(p=>p.playerId===state.profile);p.portraitId='explicit-synthetic-high-resolution';p.portraitStatus='ready';mediaCache.set(state.filter.world+'|'+p.playerId+'|'+p.portraitId,URL.createObjectURL(blob));renderProfile();await loadMedia(p.playerId);const img=document.querySelector('.armory-symbol img');await img.decode();return {natural:img.naturalHeight,shown:img.getBoundingClientRect().height};});assert(highResolution.natural>950&&highResolution.natural>highResolution.shown,'Browser preserves high resolution capture detail without downsizing to CSS size');
  await portraitPage.setViewportSize({width:1440,height:1000});await portraitPage.screenshot({path:path.join(output,'backdrop-full-high-resolution.png')});
  await portraitPage.locator('#appearance').evaluate(e=>e.open=true);await portraitPage.locator('[data-backdrop="boxed"]').click();assert.equal(await portraitPage.evaluate(()=>document.body.classList.contains('scenic-profile')),false,'Boxed comparison can be restored');assert.equal(await portraitPage.locator('[data-backdrop="boxed"]').getAttribute('aria-pressed'),'true');await portraitPage.screenshot({path:path.join(output,'backdrop-boxed-high-resolution.png')});
  // Responsive artwork has independent remembered choices, exact cover sizing and a true document-scrolling mode.
  assert.equal(await portraitPage.locator('[data-artwork]').count(),0,'Removed artwork selector');
  const sizes=await portraitPage.evaluate(()=>[backdropResolution(1280,720,1),backdropResolution(2560,1440,1),backdropResolution(1920,1080,2),backdropResolution(390,844,2),backdropResolution(5120,1440,1)]);
  assert.deepEqual(sizes,[1920,2560,3840,3840,3840],'Responsive selection accounts for both cover axes, device density and a bounded largest source');
  await portraitPage.evaluate(()=>localStorage.setItem('sagas.artwork','scenic'));
  assert((await portraitPage.locator('#loadout').evaluate(e=>e.style.getPropertyValue('--biome-art'))).includes('/grounded/'),'Grounded is the only artwork source');
  assert.equal(await portraitPage.locator('[data-backdrop="boxed"]').getAttribute('aria-pressed'),'true','Changing artwork leaves backdrop mode intact');
  assert((await portraitPage.locator('#loadout').evaluate(e=>e.style.getPropertyValue('--biome-art'))).includes('/grounded/meadows-1920.webp'),'Boxed artwork selects its actual display size');
  const shadow=await portraitPage.locator('.armory-symbol').evaluate(e=>getComputedStyle(e,'::after').content);assert.equal(shadow,'""','Grounded portrait has a subtle contact shadow');
  await portraitPage.screenshot({path:path.join(output,'backdrop-grounded-boxed.png')});
  await portraitPage.locator('#appearance').evaluate(e=>e.open=true);await portraitPage.locator('[data-backdrop="scroll"]').click();
  assert.equal(await portraitPage.locator('#profileBackdrop').evaluate(e=>getComputedStyle(e).position),'absolute','Scroll mode is anchored to the document');
  await portraitPage.evaluate(()=>scrollTo({top:0,behavior:'instant'}));const topBefore=await portraitPage.locator('#profileBackdrop').evaluate(e=>e.getBoundingClientRect().top);
  await portraitPage.evaluate(()=>scrollTo({top:260,behavior:'instant'}));const topAfter=await portraitPage.locator('#profileBackdrop').evaluate(e=>e.getBoundingClientRect().top);
  assert(Math.abs(topBefore-topAfter-260)<2,'Scroll mode scenery moves the exact document scroll distance');
  assert.equal(await portraitPage.locator('#profileBackdrop').evaluate(e=>getComputedStyle(e).backgroundRepeat.split(',').pop().trim()),'no-repeat','Artwork never repeats down long profiles');
  assert(await portraitPage.locator('#profileBackdrop').evaluate(e=>e.offsetHeight<document.body.scrollHeight),'Hero fades before the end of a long profile rather than stretching artwork');
  await portraitPage.screenshot({path:path.join(output,'backdrop-grounded-scroll.png')});
  await portraitPage.locator('[data-profile-tab="saga"]').click();assert.equal(await portraitPage.evaluate(()=>document.body.classList.contains('scrolling-profile')),true,'Scroll scenery is also visible on Saga');await portraitPage.screenshot({path:path.join(output,'backdrop-grounded-scroll-saga.png')});
  await portraitPage.locator('#appearance').evaluate(e=>e.open=true);await portraitPage.locator('[data-backdrop="full"]').click();await portraitPage.evaluate(()=>scrollTo({top:200,behavior:'instant'}));assert.equal(await portraitPage.locator('#profileBackdrop').evaluate(e=>e.getBoundingClientRect().top),0,'Full screen retains its fixed behavior while scrolling');
  await portraitPage.locator('#appearance').evaluate(e=>e.open=true);await portraitPage.locator('[data-backdrop="scroll"]').click();await portraitPage.reload();await portraitPage.waitForFunction(()=>state.profileData?.stats&&!state.busy);
  assert.equal(await portraitPage.locator('[data-backdrop="scroll"]').getAttribute('aria-pressed'),'true','Scroll preference survives reload');assert.equal(await portraitPage.evaluate(()=>localStorage.getItem('sagas.artwork')),null,'Legacy Scenic preference removed on reload');assert.equal(await portraitPage.locator('#profileSaga').isVisible(),true,'Saga direct link retains selected background');
  for(const size of [{width:360,height:780},{width:390,height:844},{width:2560,height:1440},{width:3840,height:2160}]){await portraitPage.setViewportSize(size);await portraitPage.waitForTimeout(60);assert.equal(await portraitPage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'Expanded toolbar and background fit all tested viewports');for(const selector of ['[data-backdrop="scroll"]']){const bounds=await portraitPage.locator(selector).boundingBox();assert(bounds.x>=0&&bounds.x+bounds.width<=size.width,'Presentation controls remain within mobile and desktop viewport');}const decoded=await portraitPage.evaluate(async()=>{const source=document.body.style.getPropertyValue('--profile-backdrop').match(/url\("([^"]+)"\)/)?.[1];const image=new Image();image.src=source;await image.decode();return {source,width:image.naturalWidth,height:image.naturalHeight};});assert.equal(decoded.width,Number(decoded.source.match(/-(\d+)\.webp(?:\?|$)/)[1]),'Responsive asset decodes at its advertised width');assert.equal(decoded.height,decoded.width*9/16,'Responsive scenery retains its16:9 composition');await portraitPage.screenshot({path:path.join(output,'backdrop-scroll-saga-'+size.width+'.png')});}
  await portraitPage.locator('.masthead nav a[href="#world"]').click();await portraitPage.waitForFunction(()=>document.body.dataset.page==='world');assert.equal(await portraitPage.locator('#profileBackdrop').isVisible(),false,'Leaving profile hides scroll layer');assert.equal(await portraitPage.evaluate(()=>document.body.classList.contains('grounded-profile')),false,'Leaving profile removes artwork class');
  await portraitPage.locator('.masthead nav a[href="#profile"]').click();await portraitPage.locator('#vikingDirectorySearch').fill('Astrid');await portraitPage.locator('#vikingDirectoryResults [data-profile="fixture-0"]').click();await portraitPage.waitForFunction(()=>document.body.dataset.page==='profile');await portraitPage.evaluate(()=>{state.busy=true;const p=state.data.players.find(p=>p.playerId===state.profile);p.profileBiome='';renderProfile();});assert.equal(await portraitPage.locator('#profileBackdrop').isVisible(),false,'Withheld biome cannot retain either artwork set');assert.equal(await portraitPage.locator('#loadout').evaluate(e=>e.style.getPropertyValue('--biome-art')),'none','Consent revocation clears boxed image too');
  // Explicit synthetic portrait pixels test the shared roster crop, refresh stability and privacy fallback.
  await portraitPage.locator('.masthead nav a[href="#world"]').click();
  await portraitPage.setViewportSize({width:1440,height:1050});
  await portraitPage.evaluate(async()=>{state.busy=true;state.profile='fixture-0';for(const [i,p] of state.data.players.entries()){p.online=true;p.portraitId='roster-synthetic-'+i;const c=document.createElement('canvas');c.width=100;c.height=220;const ctx=c.getContext('2d');ctx.fillStyle=i?'#65a6cb':'#d4a56d';ctx.fillRect(35,10,30,30);ctx.fillStyle='#484f50';ctx.fillRect(20,40,60,160);const blob=await new Promise(r=>c.toBlob(r));mediaCache.set(state.filter.world+'|'+p.playerId+'|'+p.portraitId,URL.createObjectURL(blob));}render();await Promise.all(state.data.players.map(p=>loadMedia(p.playerId)));await Promise.all([...document.querySelectorAll('#online img')].map(img=>img.decode()));});
  await portraitPage.locator('.masthead nav a[href="#world"]').click();
  assert.equal(await portraitPage.locator('#online .roster-avatar.has-portrait').count(),2,'Both live players show consented head avatars');
  assert.equal(await portraitPage.locator('#online .roster-avatar>span').first().evaluate(e=>getComputedStyle(e).visibility),'hidden','Loaded roster avatars hide initials');
  const stable=await portraitPage.evaluate(()=>{const img=$('online').querySelector('img'),src=img.src;render();return img===$('online').querySelector('img')&&img.src===src;});assert(stable,'Polling preserves roster image DOM and URL');
  await portraitPage.setViewportSize({width:1440,height:1050});await portraitPage.screenshot({path:path.join(output,'world-avatar-roster.png')});
  await portraitPage.setViewportSize({width:390,height:844});assert(await portraitPage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Avatar roster fits mobile');await portraitPage.screenshot({path:path.join(output,'world-avatar-roster-mobile.png')});
  await portraitPage.evaluate(()=>{state.data.players.find(p=>p.playerId==='fixture-1').portraitId='';render();});assert.equal(await portraitPage.locator('#online .player-chip[data-profile="fixture-1"]').locator('img').count(),0,'Withheld portrait removes old image');assert.equal(await portraitPage.locator('#online .player-chip[data-profile="fixture-1"]').locator('.roster-avatar>span').evaluate(e=>getComputedStyle(e).visibility),'visible','Missing portrait retains initials');
  await portraitPage.locator('#online .player-chip[data-profile="fixture-0"]').click();await portraitPage.waitForFunction(()=>location.hash.startsWith('#profile/Astrid-Ashwalker'));
  await portraitPage.evaluate(()=>{state.busy=true;const p=state.data.players.find(p=>p.playerId==='fixture-0');p.profileBiome='DeepNorth';renderProfile();});
  const north=await portraitPage.evaluate(async()=>{const source=document.body.style.getPropertyValue('--profile-backdrop').match(/url\("([^"]+)"\)/)?.[1];const im=new Image();im.src=source;await im.decode();return {source,width:im.naturalWidth};});assert(north.source.includes('/grounded/deep-north-')&&north.width>0,'Deep North renders through real static asset route');
  await require('./grounding.cjs')(portraitPage,output);
  assert.deepEqual(portraitErrors,[],'Portrait pages and real responsive artwork load without browser errors');await portraitPage.close();
  console.log('PASS: synthetic end-to-end auth, all ranges, custom dates, player filtering, live presence, World/Vikings/Leaderboard/Server saga routes and real boss/team/timing/rarity/gold/bounty rankings, independent personal presets/custom dates and tabs, actual local server-saga queue/API/browser rendering, named marker profile links, map union/zoom/terrain orientation/fog, Epic Loot colors, public/private access, authenticated portrait/icons and failure states, numbered hotbar, local Viking search, circular portrait, owner-race protection and offline art retention/reload, deduplicated tooltips, resistance display, keyboard/mouse/tap armory, escaping, truncation, responsive layout, persistent Boxed/Full screen/Scroll and Grounded artwork, true scroll motion, both Saga backgrounds, responsive WebP decoding and privacy cleanup.');
  console.log('Screenshots: '+output);
 } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exitCode=1;});
