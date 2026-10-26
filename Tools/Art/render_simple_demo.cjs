const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

(async () => {
  const dir = path.resolve(__dirname, '../../Docs/Art/directions');
  const svg = fs.readFileSync(path.join(dir, 'H_simple_forest.svg'), 'utf8');
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  try {
    const page = await browser.newPage({ viewport: { width: 2696, height: 1000 }, deviceScaleFactor: 1 });
    await page.setContent('<html><head><style>body{margin:0}svg{display:block}</style></head><body>' + svg + '</body></html>');
    await page.evaluate(() => document.fonts.ready);
    const fonts = await page.evaluate(() => [...document.fonts].map(f => ({ family: f.family, status: f.status })));
    if (fonts.some(font => font.status !== 'loaded')) throw new Error('A product font failed to load');
    await page.screenshot({ path: path.join(dir, 'H_simple_forest.png') });
    await page.screenshot({ path: path.join(dir, 'H_gameplay_1280.png'), clip: { x: 48, y: 210, width: 1280, height: 720 } });
    console.log(JSON.stringify({ output: path.join(dir, 'H_simple_forest.png'), fonts }));
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
