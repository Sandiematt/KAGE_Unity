// Frame strips of the JS UI animations at exact times: CSS animations and transitions are paused and set to a time through the Web Animations API
// (a headless screencast is too coarse). The ink wipe is replayed by hand: the stroke slides in, the screen changes under it at 235 ms, the stroke slides out.
//
//   node Tools/js_ui_capture/scrub.mjs --js E:/PROJECTS/KAGE/KAGE --out <dir> title_reveal,wipe,campaign_open,...
import path from 'node:path';
import fs from 'node:fs';
import { buildGame, launch, sleep } from './cdp.mjs';

const arg = (k, d) => { const i = process.argv.indexOf('--' + k); return i > 0 && process.argv[i + 1] != null ? process.argv[i + 1] : d; };
const jsRoot = arg('js', 'E:/PROJECTS/KAGE/KAGE');
const out = path.resolve(arg('out', 'Screenshots/review/js-anim'));
const names = (process.argv[process.argv.length - 1] || '').split(',').filter(Boolean);
const tmp = path.join(process.env.TEMP || '/tmp', 'kage-js-ui-scrub');
const U = 'KAGE.UIManager';
const MID = `(() => { const d = KAGE.SaveManager.data; d.coins = 1500; d.gems = 30; d.owned.weapons = { kagetsu: 3, tsubame: 1 }; d.owned.helmets = { shiro_maki: 1, kuro_zukin: 2 }; d.owned.armors = { tachibana_yoroi: 1, ronin_yoroi: 1 }; d.difficultyPicked = true; KAGE.SaveManager.save(); })()`;
const FRESH = `(() => { KAGE.SaveManager.data.difficultyPicked = true; KAGE.SaveManager.save(); })()`;
const clear = ids => `(() => { for (const id of ${JSON.stringify(ids)}) KAGE.Logic.completeStage(KAGE.SaveManager.data, id); })()`;
const result = `(() => { const K = KAGE, cfg = K.Game.stageConfig('act1.sub1'), st = K.Logic.stage('act1.sub1');
  const r = { mode: 'campaign', stageId: 'act1.sub1', win: true, time: 48, flawless: true, enemyId: cfg.enemyId, foeName: cfg.foeName, env: cfg.env, hp: 100, maxHp: 100, enemyDamage: 1, ng: 0, kind: st.kind, clues: 0 };
  const o = K.Logic.applyFightResult(K.SaveManager.data, r); K.Game.lastResult = o; ${U}.showResult(o); ${U}.show('result'); ${U}.runAfterShow(); })()`;
const WIPE = 0.235;
const TITLE = `${U}.renderTitle(); ${U}.show('title');`;
const SCRUB = {
  title_reveal: { dev: true, setup: TITLE, enter: `${U}.renderTitle(); ${U}.show('title'); ${U}.runAfterShow();`, times: [0, .12, .24, .4, .6, .9] },
  controls_open: { dev: true, setup: TITLE, enter: `${U}.openHelp();`, times: [0, .08, .16, .24, .35, .5] },
  toast: { dev: true, setup: TITLE, enter: `${U}.toast('Acquired', 'Onikiri')`, times: [0, .12, .25, .5, 2.62, 2.8], toast: true },
  wipe: { dev: true, setup: TITLE, swap: `${U}.renderCampaign(); ${U}.show('campaign');`, times: [0, .1, .2, .24, .34, .45] },
  campaign_open: { dev: false, setup: `${FRESH}; ${TITLE}`, swap: `${U}.selStage = 'prologue.1'; ${U}.renderCampaign(); ${U}.show('campaign');`, times: [.2, .4, .55, .7, .9, 1.3] },
  loadout_open: { dev: true, setup: `${U}.selStage = 'prologue.1'; ${U}.renderCampaign(); ${U}.show('campaign');`, swap: `${U}.preparePrefight(KAGE.Game.stageConfig('prologue.1')); ${U}.show('prefight'); ${U}.runAfterShow();`, times: [.24, .4, .55, .75, 1.0, 1.4] },
  armory_open: { dev: false, mid: true, setup: TITLE, swap: `${U}.returnTo = 'title'; ${U}.armTab = 'weapons'; ${U}.armSel = 'onikiri'; ${U}.armLine = null; ${U}.renderArmory(); ${U}.show('armory'); ${U}.runAfterShow();`, times: [.24, .4, .55, .75, 1.0, 1.5] },
  results_open: { dev: false, setup: `${FRESH}; ${clear(['prologue.1', 'act1.stretch1', 'act1.stretch2'])}`, swap: result, times: [.24, .5, .8, 1.1, 1.6, 2.4] },
};

const freeze = ms => `(() => { for (const a of document.getAnimations()) { a.pause(); a.currentTime = ${ms}; } })()`;
const cls = c => `(() => { const w = document.getElementById('wipe'); w.className = ''; void w.offsetWidth; w.className = '${c}'; })()`;

for (const name of names) {
  const s = SCRUB[name];
  if (!s) { console.error('no strip', name); continue; }
  const html = buildGame(jsRoot, path.join(tmp, s.dev ? 'dev.html' : 'plain.html'), h => s.dev ? h : h.replace('unlockAll: true', 'unlockAll: false'));
  const g = await launch(html);
  const dir = path.join(out, name);
  fs.rmSync(dir, { recursive: true, force: true }); fs.mkdirSync(dir, { recursive: true });
  try {
    await g.eval('window.dispatchEvent(new KeyboardEvent("keydown", { code: "Enter" }))');
    await sleep(1600);
    if (s.mid) await g.eval(MID);
    await g.eval(s.setup); await sleep(1500);
    let swapped = false;
    for (const t of s.times) {
      const ms = Math.round(t * 1000);
      if (s.swap) {
        if (t < WIPE) { await g.eval(cls('in')); await g.eval(freeze(ms)); }
        else {
          if (!swapped) { await g.eval(s.swap); swapped = true; await sleep(300); }
          await g.eval(cls('out')); await g.eval(freeze(Math.round((t - WIPE) * 1000)));
        }
      } else if (s.toast) {
        if (t < 2.6) { await g.eval(`document.querySelector('#toasts').innerHTML = ''; ${s.enter}`); await g.eval(freeze(ms)); }
        else { await g.eval(`document.querySelector('.toast').classList.add('out')`); await g.eval(freeze(Math.round((t - 2.6) * 1000))); }
      } else { await g.eval(s.setup); await g.eval(s.enter); await g.eval(freeze(ms)); }
      await sleep(150);
      await g.shot(path.join(dir, 'f' + String(ms).padStart(5, '0') + '.png'));
    }
    console.log('scrub', name, s.times.length, 'frames');
  } catch (e) { console.error(name, e.message); } finally { await g.close(); }
}
