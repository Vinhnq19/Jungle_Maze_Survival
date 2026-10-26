const path = require('path');
const sharp = require('sharp');
const dir = path.resolve(__dirname, '../../Assets/_JungleMaze/Art/Hud');
(async () => {
  const arc = '<svg width="88" height="232" viewBox="0 0 88 232" xmlns="http://www.w3.org/2000/svg"><path d="M8 8Q96 116 8 224H30Q112 116 30 8Z" fill="white"/></svg>';
  const arrow = '<svg width="120" height="120" viewBox="0 0 120 120" xmlns="http://www.w3.org/2000/svg"><path d="M28 18L86 60L28 102V80L57 60L28 40Z" fill="none" stroke="white" stroke-width="9" stroke-linejoin="round"/></svg>';
  await sharp(Buffer.from(arc)).png().toFile(path.join(dir, 'HudArcFilled.png'));
  await sharp(Buffer.from(arrow)).png().toFile(path.join(dir, 'HudArrowOutline.png'));
})().catch(e => { console.error(e); process.exitCode = 1; });
