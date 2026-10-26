const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  try {
    const page = await browser.newPage({ viewport: { width: 2672, height: 900 }, deviceScaleFactor: 1 });
    const svg = fs.readFileSync(path.join(__dirname, 'J_jungle_polished.svg'), 'utf8');
    await page.setContent('<html><style>body{margin:0}svg{display:block}</style>' + svg + '</html>');
    await page.evaluate(() => document.fonts.ready);
    const fonts = await page.evaluate(() => [...document.fonts].map(f => ({ family: f.family, status: f.status })));
    if (fonts.length !== 2 || fonts.some(f => f.status !== 'loaded')) throw new Error('Product fonts not loaded');
    const files = [
      ['J_jungle_polished.png', null],
      ['J_menu_1280.png', { x: 40, y: 122, width: 1280, height: 720 }],
      ['J_hud_1280.png', { x: 1352, y: 122, width: 1280, height: 720 }],
    ];
    for (const [file, clip] of files) await page.screenshot({ path: path.join(__dirname, file), ...(clip ? { clip } : {}) });
    console.log(JSON.stringify({ files: files.map(f => f[0]), fonts }));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
