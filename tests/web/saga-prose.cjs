const {chromium}=require('playwright'),assert=require('node:assert/strict');
(async()=>{const b=await chromium.launch({headless:true,executablePath:process.env.SAGAS_BROWSER_EXECUTABLE||(process.platform==='win32'?'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe':undefined)});try{
 const p=await b.newPage();await p.addInitScript(()=>sessionStorage.setItem('sagas.viewer','synthetic-development-token-only'));
 await p.goto('http://127.0.0.1:9847/#profile/fixture-0/saga');await p.waitForFunction(()=>state.profileData?.stats&&!state.busy);
 await p.waitForFunction(()=>!profileBusy&&!personalBusy&&!worldBusy&&!state.busy);await p.evaluate(()=>{state.busy=true;profileBusy=true;personalBusy=true;worldDue=Infinity;appliedPresence=++presenceSerial;clearInterval(state.timer);const chapter={playerId:state.profile,model:'synthetic-only',title:'Synthetic chapter',text:'Fictional embellishment: Across the grey water.\n\nThe last ember glowed.',utc:new Date().toISOString(),facts:['Synthetic recorded evidence'],eventIds:[]};state.profileData.chapters=[chapter];state.data.chapters=[chapter];renderProfile();});
 assert.equal(await p.locator('#chapters .prose').textContent(),'Across the grey water.\n\nThe last ember glowed.');
 assert((await p.locator('#chapters details').textContent()).includes('Synthetic recorded evidence'));
 assert(await p.evaluate(()=>state.profileData.chapters[0].text.startsWith('Fictional embellishment:')),'Stored source remains intact');
 assert(!(await p.evaluate(()=>directorySaga({playerId:state.profile}))).includes('Fictional embellishment:'));
 assert.equal(await p.evaluate(()=>sagaProse('Within this story Fictional embellishment: is quoted.')),'Within this story Fictional embellishment: is quoted.');
 assert.equal(await p.evaluate(()=>sagaProse(null)),'');
 await p.route('**/api/server-saga?*',r=>r.fulfill({json:{chapters:[{title:'Synthetic server chapter',text:'Fictional embellishment: Their separate tales found a place in the hall.',facts:['Synthetic server evidence'],participants:[]}]}}));
 await p.locator('nav a[href="#server-saga"]').click();await p.waitForFunction(()=>document.querySelector('#serverSagaChapters .prose'));
 assert.equal(await p.locator('#serverSagaChapters .prose').textContent(),'Their separate tales found a place in the hall.');
 console.log('PASS saga display: legacy prefix removed for Viking/server/directory, paragraphs and evidence preserved, source unmodified, unrelated text retained.');
}finally{await b.close();}})().catch(e=>{console.error(e);process.exit(1)});
