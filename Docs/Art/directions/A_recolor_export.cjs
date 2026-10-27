const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  try {
    const page = await browser.newPage({ viewport: { width: 2728, height: 980 }, deviceScaleFactor: 1 });
    const svg = fs.readFileSync(path.join(__dirname, 'A_charcoal_jade.svg'), 'utf8');
    await page.setContent('<html><style>body{margin:0}svg{display:block}</style>' + svg + '</html>');
    await page.evaluate(() => document.fonts.ready);
    const fonts = await page.evaluate(() => [...document.fonts].map(f => ({ family: f.family, weight: f.weight, status: f.status })));
    if (fonts.some(f => f.status !== 'loaded')) throw new Error('Source product fonts not loaded');
    for (const [name, clip] of [
      ['A_charcoal_jade.png', null],
      ['A_charcoal_jade_hud_1280.png', { x: 64, y: 236, width: 1280, height: 720 }],
      ['A_charcoal_jade_menu_1280.png', { x: 1384, y: 236, width: 1280, height: 720 }],
    ]) await page.screenshot({ path: path.join(__dirname, name), ...(clip ? { clip } : {}) });
    console.log(JSON.stringify({ fonts, board: [2728, 980], crops: [1280, 720] }));
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
