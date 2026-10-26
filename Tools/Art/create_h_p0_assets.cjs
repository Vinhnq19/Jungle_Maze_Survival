const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

// Rasterize the approved flat vector direction; retain tintable white UI masks.
const root = path.resolve(__dirname, '../..');
const svg = (size, body) => `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">${body}</svg>`;
const hud = path.join(root, 'Assets/_JungleMaze/Art/Hud');
const foundation = path.join(root, 'Assets/_JungleMaze/Art/Foundation');
async function main() {
  const masks = [
    ['HudRound10.png', 96, '<rect width="96" height="96" rx="10" fill="white"/>'],
    ['HudRound6.png', 128, '<rect width="128" height="128" rx="8" fill="white"/>'],
    ['HudRound6Stroke2.png', 128, '<rect x="1" y="1" width="126" height="126" rx="8" fill="none" stroke="white" stroke-width="2"/>'],
    ['HudRound6Dashed.png', 128, '<rect x=".5" y=".5" width="127" height="127" rx="8" fill="none" stroke="white" stroke-dasharray="5 4"/>'],
  ];
  for (const [name, size, body] of masks) await sharp(Buffer.from(svg(size, body))).png().toFile(path.join(hud, name));
  const world = fs.readFileSync(path.join(root, 'Docs/Art/directions/H_world.svg'), 'utf8');
  const hero = world.match(/<g transform="translate\(640 360\)">([\s\S]*?)<\/g>/)[1];
  await sharp(Buffer.from(svg(96, `<g transform="translate(39 43) scale(1.25)">${hero}</g>`))).png().toFile(path.join(foundation, 'Hero48.png'));
  await sharp(Buffer.from(svg(96, '<rect width="96" height="96" fill="white"/>'))).png().toFile(path.join(foundation, 'Cell48.png'));
  await sharp(Buffer.from(svg(96, '<rect width="96" height="96" fill="#5B6D4B"/>'))).png().toFile(path.join(foundation, 'Ground96.png'));
  console.log('Created H P0 sprites; shared Cell48 stays a white tint mask.');
}
main().catch(e => { console.error(e); process.exitCode = 1; });
