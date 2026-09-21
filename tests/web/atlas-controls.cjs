// Open controls through their public UI, preserving real browser interactions.
exports.revealAtlas=async function(page,key){
 if(key==='filters'){if(!await page.locator('.activity-filters').getAttribute('open')){const open=await page.locator('.activity-filters').evaluate(e=>e.open);if(!open)await page.locator('.activity-filters>summary').click();}return;}
 const button=page.locator('[data-atlas-tray="'+key+'"]');if(await button.getAttribute('aria-expanded')!=='true')await button.click();
};
