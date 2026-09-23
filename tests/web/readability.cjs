/* Synthetic browser presentation checks; requires the isolated DevHost. */
const {chromium}=require('playwright');const assert=require('node:assert/strict');
const url=process.env.SAGAS_TEST_URL||'http://127.0.0.1:9847/';
if(!/^http:\/\/(127\.0\.0\.1|localhost):\d+\/$/.test(url))throw Error('Tests require an isolated loopback DevHost.');
(async()=>{const browser=await chromium.launch({headless:true,executablePath:process.env.SAGAS_BROWSER_EXECUTABLE||(process.platform==='win32'?'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe':undefined)});
try{const page=await browser.newPage({viewport:{width:1440,height:1050}});await page.addInitScript(()=>sessionStorage.setItem('sagas.viewer','synthetic-development-token-only'));
await page.goto(url+'#profile/fixture-0');await page.waitForFunction(()=>state.profileData?.stats&&!state.busy);await page.waitForLoadState('networkidle');
// Freeze polling while intentionally changing the synthetic presence snapshot.
await page.evaluate(()=>{state.busy=true;worldDue=Infinity;appliedPresence=++presenceSerial;const p=state.data.players.find(p=>p.playerId===state.profile);p.hotbar=p.gear.map((g,i)=>({...g,hotbarSlot:i+1}));p.online=true;p.lastSeenUtc=new Date().toISOString();renderProfile();});
assert.equal(await page.locator('#characterStatus .profile-presence.online').textContent(),'Online now');
await page.evaluate(()=>{const p=state.data.players.find(p=>p.playerId===state.profile);p.lastSeenUtc=new Date(Date.now()-60000).toISOString();renderProfile();});
assert.equal(await page.locator('#characterStatus .profile-presence.delayed').textContent(),'Online / syncing');
await page.evaluate(()=>{const p=state.data.players.find(p=>p.playerId===state.profile);p.online=false;renderProfile();});
assert.equal(await page.locator('#characterStatus .profile-presence.offline').textContent(),'Offline');
assert.match(await page.locator('#characterStatus .presence-detail').textContent(),/^Last seen /);
for(const [width,expectedIcon,columns] of [[1440,48,8],[390,44,4]]){await page.setViewportSize({width,height:1050});
const sizes=await page.evaluate(()=>{const css=s=>getComputedStyle(document.querySelector(s));return{hotbarIcon:parseFloat(css('.hotbar-slot .item-icon').width),hotbarFont:parseFloat(css('.hotbar-slot small').fontSize),gearIcon:parseFloat(css('.gear-slot .item-icon').width),gearFont:parseFloat(css('.gear-slot strong').fontSize),columns:css('#hotbar').gridTemplateColumns.split(' ').length,overflow:document.documentElement.scrollWidth>innerWidth};});
assert.equal(sizes.hotbarIcon,expectedIcon);assert.equal(sizes.columns,columns);assert.ok(sizes.hotbarFont>=12.8);assert.ok(sizes.gearIcon>=38);assert.ok(sizes.gearFont>=12.8);assert.equal(sizes.overflow,false,'Readable equipment fits the viewport');}
await page.setViewportSize({width:1440,height:1050});await page.locator('#characterSearch').focus();
assert.equal(await page.locator('#characterSearch').evaluate(e=>getComputedStyle(e).outlineOffset),'-3px');
await page.evaluate(()=>{$('worldLabel').hidden=false;$('world').focus();});
assert.equal(await page.locator('#world').evaluate(e=>getComputedStyle(e).outlineOffset),'-3px');
await page.locator('#appearance').evaluate(e=>e.open=true);await page.locator('[data-backdrop="full"]').click();
assert.notEqual(await page.locator('.profile-toolbar').evaluate(e=>getComputedStyle(e).backgroundImage),'none');
console.log('PASS profile presence states, desktop/mobile item readability, no horizontal overflow, inset field focus and fullscreen text support.');
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exit(1)});
