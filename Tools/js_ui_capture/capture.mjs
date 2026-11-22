// Takes the JS game's UI screens and animation frame strips for the side-by-side review (SAN-67 and SAN-90).
//
//   node Tools/js_ui_capture/capture.mjs --js E:/PROJECTS/KAGE/KAGE --out Screenshots/review/SAN-67/js shots title,campaign_prologue,...
//   node Tools/js_ui_capture/capture.mjs --js E:/PROJECTS/KAGE/KAGE --out <dir> strips title_reveal,wipe,splash,...
//
// `shots` writes <out>/<name>.png (1600 x 900); `strips` writes every frame it recorded to <out>/<name>/f<ms>.png (screencast frames
// with their time in ms since the action started; the splash is stepped by hand at exact times). It builds a private copy of the game
// (dev unlock on, or off with the "mid" saves), so it never touches the JS repo's index.html. Needs Microsoft Edge.
import path from 'node:path';
import fs from 'node:fs';
import { buildGame, launch, sleep } from './cdp.mjs';

const arg = (k, d) => { const i = process.argv.indexOf('--' + k); return i > 0 && process.argv[i + 1] != null ? process.argv[i + 1] : d; };
const jsRoot = arg('js', 'E:/PROJECTS/KAGE/KAGE');
const out = path.resolve(arg('out', 'Screenshots/review/js'));
const mode = process.argv.find(a => a === 'shots' || a === 'strips');
const names = (process.argv[process.argv.indexOf(mode) + 1] || '').split(',').filter(Boolean);
const tmp = path.join(process.env.TEMP || '/tmp', 'kage-js-ui-capture');

// the mid-game save of the SAN-90 review: 1500 coins, 30 gems, Kagetsu 3, Tsubame, Kuro Zukin 2, Ronin Armor
const MID = `(() => { const d = KAGE.SaveManager.data; d.coins = 1500; d.gems = 30; d.owned.weapons = { kagetsu: 3, tsubame: 1 };
  d.owned.helmets = { shiro_maki: 1, kuro_zukin: 2 }; d.owned.armors = { tachibana_yoroi: 1, ronin_yoroi: 1 }; d.difficultyPicked = true; KAGE.SaveManager.save(); })()`;
const FRESH = `(() => { const d = KAGE.SaveManager.data; d.difficultyPicked = true; KAGE.SaveManager.save(); })()`;
const U = 'KAGE.UIManager';
const showAr = (tab, id, line) => `${U}.returnTo = 'title'; ${U}.armTab = '${tab}'; ${U}.armSel = '${id}'; ${U}.armLine = ${line ? `'${line}'` : 'null'}; ${U}.renderArmory(); ${U}.show('armory'); ${U}.runAfterShow();`;
const clearStages = (ids) => `(() => { for (const id of ${JSON.stringify(ids)}) KAGE.Logic.completeStage(KAGE.SaveManager.data, id); })()`;
const fakeResult = (stage, win) => `(() => { const K = KAGE, st = K.Logic.stage('${stage}'), cfg = K.Game.stageConfig('${stage}');
  const r = { mode: 'campaign', stageId: '${stage}', win: ${win}, time: ${win ? 48 : 70}, flawless: ${win}, enemyId: cfg.enemyId, foeName: cfg.foeName, env: cfg.env, hp: 100, maxHp: 100, enemyDamage: ${win ? 1 : 0.62}, ng: 0, kind: st.kind, clues: 0 };
  const o = K.Logic.applyFightResult(K.SaveManager.data, r); K.Game.lastResult = o; ${U}.showResult(o); ${U}.show('result'); ${U}.runAfterShow(); })()`;

// SAN-67 stretch cleared: the first stretch of Act 1, past its intro, then the JS finishStretch (the brush banner and the outro)
const startRoad = `${U}.go('hud', () => KAGE.Game.startFight(KAGE.Game.stageConfig('act1.stretch1'), { skipBefore: true })); new Promise(r => setTimeout(r, 3400))`;
const finishRoad = `KAGE.Game.session.finishStretch()`;

// every shot: which build (dev on or off), the page script that sets the screen up, how long to let it settle
const SHOTS = {
  title: { dev: true, run: `${U}.renderTitle(); ${U}.show('title'); ${U}.runAfterShow();`, wait: 2200 },
  campaign_prologue: { dev: false, run: `${FRESH}; ${U}.selStage = 'prologue.1'; ${U}.renderCampaign(); ${U}.show('campaign');`, wait: 1600 },
  campaign_act1: { dev: false, run: `${FRESH}; ${clearStages(['prologue.1', 'act1.stretch1'])}; ${U}.selStage = 'act1.stretch2'; ${U}.renderCampaign(); ${U}.show('campaign');`, wait: 1600 },
  campaign_act1_boss: { dev: true, run: `${U}.selStage = 'act1.boss'; ${U}.renderCampaign(); ${U}.show('campaign');`, wait: 2600 },
  settings: { dev: true, run: `${U}.renderSettings(); ${U}.show('settings');`, wait: 1600 },
  loadout: { dev: true, run: `${U}.preparePrefight(KAGE.Game.stageConfig('prologue.1')); ${U}.show('prefight'); ${U}.runAfterShow();`, wait: 2200 },
  loadout_raikon: { dev: true, run: `${U}.preparePrefight(KAGE.Game.stageConfig('act1.boss')); ${U}.show('prefight'); ${U}.runAfterShow();`, wait: 2200 },
  results_win: { dev: false, run: `${FRESH}; ${clearStages(['prologue.1', 'act1.stretch1', 'act1.stretch2'])}; ${fakeResult('act1.sub1', true)}`, wait: 3200 },
  results_loss: { dev: true, run: fakeResult('act1.boss', false), wait: 3200 },
  controls_guide: { dev: true, run: `${U}.renderSettings(); ${U}.show('settings'); ${U}.openHelp();`, wait: 1200 },
  profile_records: { dev: true, run: `${U}.renderTitle(); ${U}.show('title'); ${U}.openProfile();`, wait: 1200 },
  profile_trophies: { dev: true, run: `${U}.renderTitle(); ${U}.show('title'); ${U}.openTrophies();`, wait: 1200 },
  profile_loadout: { dev: true, run: `${U}.renderTitle(); ${U}.show('title'); ${U}.openLoadout();`, wait: 1500 },
  mode_picker: { dev: false, run: `${U}.renderTitle(); ${U}.show('title'); ${U}.openModePicker();`, wait: 1200 },
  toast: { dev: true, run: `${U}.renderTitle(); ${U}.show('title'); ${U}.toast('Acquired', 'Onikiri'); setTimeout(() => ${U}.toast('Level up', 'Kage reaches level 2'), 250);`, wait: 1000 },
  stretch_cleared: { dev: true, run: `${startRoad}.then(() => { ${finishRoad}; return new Promise(r => setTimeout(r, 650)); })`, wait: 0 },
  weapons_buy: { dev: false, mid: true, run: showAr('weapons', 'onikiri'), wait: 2000 },
  weapons_upgrade: { dev: false, mid: true, run: showAr('weapons', 'kagetsu'), wait: 2000 },
  weapons_locked: { dev: false, mid: true, run: showAr('weapons', 'shinsen'), wait: 2000 },
  helmets_buy: { dev: false, mid: true, run: showAr('helmets', 'oni_menpo'), wait: 2000 },
  armors_line: { dev: false, mid: true, run: showAr('armors', 'ronin_yoroi', 'line'), wait: 2000 },
  armors_trophy: { dev: false, mid: true, run: showAr('armors', 'ikazuchi_do', 'trophy'), wait: 2000 },
};

const STRIPS = {
  // the title screen opening: the CSS rises, the hero fade, the petals and clouds
  title_reveal: { dev: true, setup: `${U}.renderTitle(); ${U}.show('title'); ${U}.runAfterShow();`, action: `${U}.renderTitle(); ${U}.show('title'); ${U}.runAfterShow();`, ms: 1400 },
  // the ink wipe from the title to the campaign map
  wipe: { dev: true, setup: `${U}.renderTitle(); ${U}.show('title');`, action: `${U}.action('campaign', document.querySelector('[data-a=campaign]'))`, ms: 1300 },
  campaign_open: { dev: false, setup: `${FRESH}; ${U}.renderTitle(); ${U}.show('title');`, action: `${U}.action('campaign', document.querySelector('[data-a=campaign]'))`, ms: 1500 },
  loadout_open: { dev: true, setup: `${U}.selStage = 'prologue.1'; ${U}.renderCampaign(); ${U}.show('campaign');`, action: `${U}.action('fight-stage', document.body)`, ms: 1600 },
  results_open: { dev: false, setup: `${FRESH}; ${clearStages(['prologue.1', 'act1.stretch1', 'act1.stretch2'])}`, action: fakeResult('act1.sub1', true).replace(`${U}.showResult(o); ${U}.show('result'); ${U}.runAfterShow();`, `${U}.go('result', () => ${U}.showResult(o));`), ms: 3000 },
  armory_open: { dev: false, mid: true, setup: `${U}.renderTitle(); ${U}.show('title');`, action: `${U}.action('armory', document.querySelector('[data-a=armory]'))`, ms: 1700 },
  controls_open: { dev: true, setup: `${U}.renderTitle(); ${U}.show('title');`, action: `${U}.openHelp()`, ms: 900 },
  toast: { dev: true, setup: `${U}.renderTitle(); ${U}.show('title');`, action: `${U}.toast('Acquired', 'Onikiri')`, ms: 3300 },
  // the brush banner of a cleared stretch: painted in over a third of a second, held, faded out by 1.55 s
  stretch_cleared: { dev: true, setup: startRoad, action: finishRoad, ms: 1900 },
};

const BUILDS = {};
async function open(dev, withMid, extraInit) {
  const key = dev ? 'dev' : 'plain';
  const html = BUILDS[key] || (BUILDS[key] = buildGame(jsRoot, path.join(tmp, key + '.html'), h => dev ? h : h.replace('unlockAll: true', 'unlockAll: false')));
  // a fresh storage every time: the page's origin is file://, so the save lives in its localStorage, wiped by the new profile directory
  const g = await launch(html, { init: extraInit });
  await g.eval('window.dispatchEvent(new KeyboardEvent("keydown", { code: "Enter" }))');
  await sleep(1600);
  if (withMid) await g.eval(MID);
  return g;
}

if (mode === 'shots') {
  for (const name of names) {
    const s = SHOTS[name];
    if (!s) { console.error('no shot', name); continue; }
    const g = await open(s.dev, s.mid);
    try {
      await g.eval(s.run);
      await sleep(s.wait || 1500);
      await g.shot(path.join(out, name + '.png'));
      console.log('shot', name);
    } catch (e) { console.error(name, e.message); } finally { await g.close(); }
  }
} else if (mode === 'strips') {
  for (const name of names) {
    if (name === 'splash') { await splash(); continue; }
    const s = STRIPS[name];
    if (!s) { console.error('no strip', name); continue; }
    const g = await open(s.dev, s.mid);
    try {
      await g.eval(s.setup);
      await sleep(1200);
      const frames = await g.record(() => g.eval(s.action), s.ms);
      const dir = path.join(out, name);
      fs.rmSync(dir, { recursive: true, force: true });
      fs.mkdirSync(dir, { recursive: true });
      for (const f of frames) fs.writeFileSync(path.join(dir, 'f' + String(Math.round(f.t)).padStart(5, '0') + '.png'), Buffer.from(f.data, 'base64'));
      console.log('strip', name, frames.length, 'frames');
    } catch (e) { console.error(name, e.message); } finally { await g.close(); }
  }
}

// the splash is drawn from requestAnimationFrame with the clock it is handed: replace both with a clock stepped by hand, so each frame is at an exact time
async function splash() {
  const init = `(() => { let vt = 0; const cbs = []; performance.now = () => vt;
    window.requestAnimationFrame = cb => { cbs.push(cb); return cbs.length; };
    window.__step = ms => { vt += ms; const run = cbs.splice(0); for (const cb of run) cb(vt); }; })()`;
  const html = buildGame(jsRoot, path.join(tmp, 'plain.html'), h => h.replace('unlockAll: true', 'unlockAll: false'));
  const g = await launch(html, { init });
  try {
    await sleep(2500);                       // images and fonts load; the splash's clock starts at the first step
    await g.eval('window.__step(0)');
    const dir = path.join(out, 'splash');
    fs.rmSync(dir, { recursive: true, force: true });
    fs.mkdirSync(dir, { recursive: true });
    const times = [0, 300, 700, 1100, 1500, 1900, 2300, 2700, 3100, 3600];
    let t = 0;
    for (const ms of times) {
      if (ms > t) { for (let k = 0; k < Math.round((ms - t) / 16); k++) await g.eval('window.__step(16)'); t = ms; }
      await g.eval('window.__step(1)');
      await g.shot(path.join(dir, 'f' + String(ms).padStart(5, '0') + '.png'));
    }
    console.log('strip splash', times.length, 'frames');
  } finally { await g.close(); }
}
