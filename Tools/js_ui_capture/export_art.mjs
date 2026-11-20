// Renders the JS UI's own drawings of the gear to PNG for the Armory (SAN-67 rework): the weapon icons the JS paints into its tiles
// (UIManager.paintIcon), the helmet and armour icons, the big weapon illustration of the preview (startPreview) and the ghost warrior
// behind it. The JS draws these in code on a canvas; Unity shows the same pictures, so a tile shows the item the way the JS does.
//
//   node Tools/js_ui_capture/export_art.mjs --js E:/PROJECTS/KAGE/KAGE --out Assets/Kage/Resources/UI/Img
//
// Files: icon_w_<weapon>.png (only weapons without a JS render in ui_w_*), icon_h_<helmet>.png, icon_a_<armour>.png,
// big_w_<weapon>.png (the preview illustration, unrotated, origin at 350/600 x 300/600 of a 1200 x 600 canvas),
// armory_ghost.png (the faint warrior, 1000 x 640), splash_scene.png (the splash's painted scene with Kage recoloured by the JS code),
// splash_k.png .. splash_e.png (the four letters, converted from webp by the caller).
import path from 'node:path';
import fs from 'node:fs';
import { buildGame, launch, sleep } from './cdp.mjs';

const arg = (k, d) => { const i = process.argv.indexOf('--' + k); return i > 0 && process.argv[i + 1] != null ? process.argv[i + 1] : d; };
const jsRoot = arg('js', 'E:/PROJECTS/KAGE/KAGE');
const out = path.resolve(arg('out', 'Assets/Kage/Resources/UI/Img'));
const tmp = path.join(process.env.TEMP || '/tmp', 'kage-js-ui-export');
fs.mkdirSync(out, { recursive: true });

const html = buildGame(jsRoot, path.join(tmp, 'game.html'), h => h.replace(/return \{\s*start\(\) \{/, 'return {\n    get imgs() { return imgs; },\n    start() {')
  .replace('window.KAGE = {', 'window.KAGE = { HELMETS, ARMORS, SHAPE, C, twinShape, drawWeaponShape, prepCanvas, makePlayerFighter, poseFighter, Splash, '));
const PRE = 'const { WEAPONS, HELMETS, ARMORS, SHAPE, C, twinShape, drawWeaponShape, prepCanvas, makePlayerFighter, poseFighter, Splash } = KAGE; ';
const g = await launch(html, { width: 1600, height: 900 });
await g.send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 900, deviceScaleFactor: 3, mobile: false });
const save = (name, dataUrl) => { fs.writeFileSync(path.join(out, name), Buffer.from(dataUrl.split(',')[1], 'base64')); };
try {
  await sleep(2500);
  await g.eval('window.dispatchEvent(new KeyboardEvent("keydown", { code: "Enter" }))');
  await sleep(1200);
  // the lists, from the JS tables
  const lists = await g.eval(`(() => { ${PRE} return {
    weapons: Object.keys(WEAPONS).filter(id => !WEAPONS[id].enemy && id !== 'fists'),
    helmets: Object.keys(HELMETS).filter(id => !HELMETS[id].enemy),
    armors: Object.keys(ARMORS).filter(id => !ARMORS[id].enemy),
  }; })()`);
  console.log(lists.weapons.length, 'weapons', lists.helmets.length, 'helmets', lists.armors.length, 'armours');
  // an icon: the JS paints it into a canvas of the tile's size; here the same call, into a canvas in the page, 3 x the pixels
  const icon = (kind, id, w, h) => g.eval(`(() => { const cv = document.createElement('canvas'); cv.style.cssText = 'position:fixed;left:0;top:0;width:${w}px;height:${h}px;z-index:99999';
    document.body.appendChild(cv); KAGE.UIManager.paintIcon(cv, '${kind}', '${id}', true); const u = cv.toDataURL('image/png'); cv.remove(); return u; })()`);
  for (const id of lists.weapons) save(`icon_w_${id}.png`, await icon('weapons', id, 100, 58));
  for (const id of lists.helmets) save(`icon_h_${id}.png`, await icon('helmets', id, 100, 58));
  for (const id of lists.armors) save(`icon_a_${id}.png`, await icon('armors', id, 100, 58));
  // the weapon illustration of the preview, drawn the way startPreview draws it at its rest pose
  for (const id of lists.weapons) {
    const u = await g.eval(`(() => { ${PRE} const id = '${id}', save = KAGE.SaveManager.data;
      const cv = document.createElement('canvas'); cv.style.cssText = 'position:fixed;left:0;top:0;width:1200px;height:600px;z-index:99999'; document.body.appendChild(cv);
      const { g, w, h } = prepCanvas(cv);
      const f = makePlayerFighter(save, { weapon: id }); poseFighter(f, null, 30);
      const L = Math.min(1000 * 0.78, (WEAPONS[id].len || 80) * 4.1), sh = SHAPE[id];
      g.save(); g.translate(350, 300);
      if (sh === 'short') { g.save(); g.translate(0, 18); drawWeaponShape(g, twinShape(WEAPONS[id], sh, 0), L * 0.82, 'art', 3, C.ink); g.restore(); g.save(); g.translate(30, -30); g.rotate(0.25); drawWeaponShape(g, twinShape(WEAPONS[id], sh, 1), L * 0.82, 'art', 3, C.ink); g.restore(); }
      else drawWeaponShape(g, twinShape(WEAPONS[id], sh, 0), L, 'art', 3, C.ink, f);
      g.restore(); const u = cv.toDataURL('image/png'); cv.remove(); return u; })()`);
    save(`big_w_${id}.png`, u);
  }
  // the ghost warrior behind the weapon
  save('armory_ghost.png', await g.eval(`(() => { ${PRE} const save = KAGE.SaveManager.data;
    const cv = document.createElement('canvas'); cv.style.cssText = 'position:fixed;left:0;top:0;width:1000px;height:640px;z-index:99999'; document.body.appendChild(cv);
    const { g, w, h } = prepCanvas(cv);
    const ghost = makePlayerFighter(save, { weapon: 'fists', scarf: false, helmet: 'shiro_maki', armor: 'tachibana_yoroi' }); poseFighter(ghost, 'taunt', 30);
    const sc = h * 0.95 / ghost.height();
    g.save(); g.translate(w * 0.42, h * 0.99); g.scale(sc, sc); g.translate(-ghost.x, 0); ghost.draw(g, { color: '#1d0c0a', shadow: false }); g.restore();
    const u = cv.toDataURL('image/png'); cv.remove(); return u; })()`));
  // the splash's painted scene with Kage's look drawn in by the JS recolour (the first frame builds it)
  await g.eval('location.reload()');
  await sleep(3500);
  const scene = await g.eval(`(() => { ${PRE} const im = Splash.imgs.kage; return im ? im.toDataURL('image/png') : null; })()`);
  if (scene) save('splash_scene.png', scene); else console.error('no splash scene');
  console.log('done');
} finally { await g.close(); }
