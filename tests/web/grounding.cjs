const assert=require('node:assert/strict');
const path=require('node:path');
module.exports=async(page,output)=>{
  await page.locator('[data-profile-tab="character"]').click();
  await page.waitForFunction(()=>!document.querySelector('#profileCharacter').hidden);
  const biomes=['Meadows','BlackForest','Swamp','Mountain','Plains','Mistlands','Ashlands','DeepNorth'];
  for(const viewport of [{width:390,height:844},{width:1440,height:1000},{width:2560,height:1440},{width:3440,height:1440}]){
    await page.setViewportSize(viewport);
    for(const mode of ['boxed','full','scroll']){
      await page.locator('#appearance').evaluate(e=>e.open=true);await page.locator('[data-backdrop="'+mode+'"]').click();
      await page.evaluate(()=>scrollTo({top:0,behavior:'instant'}));
      for(const biome of biomes){
        const result=await page.evaluate(async biome=>{
          state.busy=true;const p=state.data.players.find(p=>p.playerId===state.profile);p.profileBiome=biome;renderProfile();await loadMedia(p.playerId);
          const img=document.querySelector('.armory-symbol img');await img.decode();renderBackdrop();
          const full=backdropMode!=='boxed',target=full?$('profileBackdrop'):$('loadout'),r=img.getBoundingClientRect(),t=target.getBoundingClientRect(),fit=Math.min(r.width/img.naturalWidth,r.height/img.naturalHeight),h=img.naturalHeight*fit;
          const foot=r.top+(r.height-h)/2+h*(1-1/42),size=target.style.getPropertyValue('--ground-size').split(' ').map(parseFloat),pos=target.style.getPropertyValue('--ground-position').split(' ').map(parseFloat),paintGround=t.top+pos[1]+size[1]*biomeGround[$('loadout').dataset.biome];
          const shadow=document.querySelector('.armory-symbol'),shadowY=shadow.getBoundingClientRect().top+parseFloat(shadow.style.getPropertyValue('--foot-top'));
          const source=(full?document.body.style.getPropertyValue('--profile-backdrop'):$('loadout').style.getPropertyValue('--biome-art')).match(/url\("([^"]+)"\)/)[1],art=new Image();art.src=source;await art.decode();
          const fadeStart=parseFloat(target.style.getPropertyValue('--ground-fade-start')),fadeEnd=parseFloat(target.style.getPropertyValue('--ground-fade-end'));
          return {fadeStart,fadeEnd,visibleHeight:t.height,foot,paintGround,shadowY,left:pos[0],right:pos[0]+size[0],width:t.width,overflow:document.documentElement.scrollWidth>innerWidth};
        },biome);
        assert(Math.abs(result.foot-result.paintGround)<1,`${biome} ${mode} ${viewport.width}: foreground meets portrait baseline`);
        assert(Math.abs(result.foot-result.shadowY)<1,'Contact shadow follows displayed bitmap, including object-fit padding');
        assert(result.fadeStart>=0&&result.fadeStart<result.fadeEnd&&result.fadeEnd<=result.visibleHeight+.1,'Bottom fade reaches opaque before the visible backdrop ends');
        assert(result.left<=.1&&result.right>=result.width-.1,'Artwork covers both horizontal edges');assert(!result.overflow,'Grounding never causes horizontal overflow');
        if(mode==='scroll'&&viewport.width===1440)await page.screenshot({path:path.join(output,'grounded-'+biome+'.png')});
      }
    }
  }
  await page.setViewportSize({width:1440,height:1000});
  await page.evaluate(()=>{renderBackdrop();scrollTo({top:200,behavior:'instant'});});
  const before=await page.locator('#profileBackdrop').evaluate(e=>({top:e.getBoundingClientRect().top,position:e.style.getPropertyValue('--ground-position')}));
  await page.evaluate(()=>{renderProfile();renderBackdrop();});
  const after=await page.locator('#profileBackdrop').evaluate(e=>({top:e.getBoundingClientRect().top,position:e.style.getPropertyValue('--ground-position')}));
  assert.deepEqual(after,before,'Polling while scrolled preserves the document ground anchor');
  await page.locator('[data-profile-tab="saga"]').click();
  assert.equal(await page.locator('#profileBackdrop').isVisible(),true,'Saga preserves scenery after grounding');
  console.log('PASS: 96 biome/mode/viewport grounding combinations, real artwork decode, contact shadows, horizontal coverage and scroll/poll stability (synthetic portrait).');
};
