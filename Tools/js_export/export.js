#!/usr/bin/env node
'use strict';
/*
 * KAGE JS content export (ADR-0010).
 *
 * Loads the JS reference game with its own headless loader (tests/helpers/load-sim.js), so every value is read AFTER all
 * JS modules have loaded (later modules change entries: Garan's health, sub-boss ability lists), and writes
 * Assets/Kage/Rules/Generated/JsTables.g.cs. Nothing here is typed by hand: to change a JS value, change the JS game and
 * run this script again. The generated file is never edited by hand.
 *
 *   node Tools/js_export/export.js [--js <path to the JS game>] [--out <output file>] [--check]
 *
 * --js    the JS game checkout (default: $KAGE_JS_DIR, else E:\PROJECTS\KAGE\KAGE). Read only.
 * --out   the C# file to write (default: Assets/Kage/Rules/Generated/JsTables.g.cs next to this repo's root).
 * --check write nothing; exit 1 when the file on disk differs from what would be generated.
 *
 * The output is deterministic (no timestamp), so a re-run on the same JS commit gives an identical file.
 * The row types are in Assets/Kage/Rules/JsTableTypes.cs.
 */
const fs = require('node:fs');
const path = require('node:path');
const cp = require('node:child_process');

const REPO_ROOT = path.resolve(__dirname, '..', '..');
const args = process.argv.slice(2);
const opt = (name) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : null; };
const JS_DIR = path.resolve(opt('--js') || process.env.KAGE_JS_DIR || 'E:\\PROJECTS\\KAGE\\KAGE');
const OUT = path.resolve(opt('--out') || path.join(REPO_ROOT, 'Assets', 'Kage', 'Rules', 'Generated', 'JsTables.g.cs'));
const CHECK = args.includes('--check');

// The chapters this scope ports (the Prologue and Act I). Everything the stages use is exported with them.
const CHAPTERS = ['prologue', 'act1'];

const { loadSim } = require(path.join(JS_DIR, 'tests', 'helpers', 'load-sim.js'));
const js = loadSim({ seed: 1, exports: ['HELMETS', 'ARMORS', 'GEAR_MAX', 'GEAR_TIERS', 'CAMPAIGN', 'AI_PROFILES', 'TUTORIAL_STEPS', 'DEV'] });

/* ---------------- C# literal helpers ---------------- */
const pascal = (k) => k[0].toUpperCase() + k.slice(1);
function str(s) {
  let o = '"';
  for (const ch of String(s)) {
    const c = ch.codePointAt(0);
    if (ch === '"') o += '\\"';
    else if (ch === '\\') o += '\\\\';
    else if (c < 0x20 || c > 0x7e) o += c > 0xffff ? '\\U' + c.toString(16).padStart(8, '0') : '\\u' + c.toString(16).padStart(4, '0');
    else o += ch;
  }
  return o + '"';
}
function num(n) {
  if (!Number.isFinite(n)) throw new Error('non-finite number ' + n);
  return String(n);
}
const strArray = (a) => a.length ? `new string[] { ${a.map(str).join(', ')} }` : 'new string[0]';
const numArray = (a, type) => `new ${type}[] { ${a.map(num).join(', ')} }`;
const IND = '    ';
// A tidy multi-line object initializer.
function obj(type, pairs, level) {
  const items = pairs.filter(([, v]) => v !== undefined && v !== null && v !== false).map(([k, v]) => `${k} = ${v}`);
  const pad = IND.repeat(level + 1), end = IND.repeat(level);
  if (!items.length) return `new ${type}()`;
  return `new ${type}\n${end}{\n${items.map((i) => pad + i).join(',\n')}\n${end}}`;
}
function arr(type, rows, level) {
  if (!rows.length) return `new ${type}[0]`;
  const pad = IND.repeat(level + 1), end = IND.repeat(level);
  return `new ${type}[]\n${end}{\n${rows.map((r) => pad + r).join(',\n')}\n${end}}`;
}
const scalar = (v) => v === undefined || v === null ? undefined : typeof v === 'string' ? str(v) : typeof v === 'boolean' ? (v ? 'true' : undefined) : typeof v === 'number' ? num(v) : (() => { throw new Error('not a scalar: ' + JSON.stringify(v)); })();
const truthy = (v) => v ? 'true' : undefined;
const pick = (o, keys) => keys.map((k) => [pascal(k), scalar(o[k])]);

/* ---------------- tables ---------------- */
function weaponRow(id, w, level) {
  return obj('JsWeapon', [
    ['Id', str(id)], ...pick(w, ['name', 'cls', 'tab', 'tier', 'base', 'stance', 'hint', 'desc', 'lore', 'price', 'gems', 'len']),
    ['Hands', w.hands === undefined ? undefined : str(String(w.hands))],
    ['Stats', numArray(w.stats, 'int')],
    ['Exclusive', truthy(w.exclusive)], ['Enemy', truthy(w.enemy)],
  ], level);
}
function perks(p) {
  const e = Object.entries(p || {});
  return e.length ? `new Dictionary<string, double> { ${e.map(([k, v]) => `{ ${str(k)}, ${num(v)} }`).join(', ')} }` : undefined;
}
function gearRow(id, g, level) {
  return obj('JsGear', [
    ['Id', str(id)], ...pick(g, ['name', 'tier', 'look', 'hue', 'hint', 'givenBy', 'lore', 'def', 'hp', 'price', 'gems']),
    ['Perks', perks(g.perks)],
    ['Exclusive', truthy(g.exclusive)], ['Enemy', truthy(g.enemy)], ['Retired', truthy(g.retired)], ['NgOnly', truthy(g.ngOnly)],
  ], level);
}
function enemyRow(id, e, level) {
  const b = e.build || {};
  return obj('JsEnemy', [
    ['Id', str(id)], ...pick(e, ['name', 'title', 'type', 'family', 'unit', 'rank', 'outfit', 'ai', 'weapon', 'helmet', 'armor', 'hp', 'def', 'dmg', 'speed',
      'eye', 'accent', 'longScarf', 'defeat', 'taunt', 'tip', 'lore', 'defeatLine', 'win', 'flashEyes']),
    ['Build', obj('JsBuild', pick(b, ['scale', 'w', 'legs', 'head', 'arms']), level + 1)],
    ['HeavyArmor', truthy(e.heavyArmor)], ['NoGrab', truthy(e.noGrab)], ['Tutorial', truthy(e.tutorial)],
    ['Phases', e.phases ? arr('JsPhase', e.phases.map((p) => obj('JsPhase', [
      ['Name', str(p.name)], ['At', scalar(p.at)], ['Abilities', strArray(p.abilities || [])],
      ['Aggressive', truthy(p.aggressive)], ['Berserk', truthy(p.berserk)], ['SecondWave', scalar(p.secondWave)],
    ], level + 2)), level + 1) : undefined],
    ['Abilities', e.abilities ? strArray(e.abilities) : undefined],
  ], level);
}
function aiRow(id, a, level) {
  return obj('JsAiProfile', [
    ['Id', str(id)], ...pick(a, ['pref', 'aggr', 'block', 'parry', 'dodge', 'jump', 'punish', 'grab', 'kunai', 'dashIn']),
    ['React', numArray(a.react, 'double')], ['Combo', numArray(a.combo, 'int')],
    ['Ranged', truthy(a.ranged)], ['Heavy', truthy(a.heavy)],
  ], level);
}
function rewardObj(r, level) {
  if (!r) return undefined;
  const list = (k) => (r[k] && r[k].length ? strArray(r[k]) : undefined);
  return obj('JsReward', [['Weapons', list('weapons')], ['Helmets', list('helmets')], ['Armors', list('armors')]], level);
}
function stretchObj(s, level) {
  if (!s) return undefined;
  return obj('JsStretch', [
    ...pick(s, ['length', 'start', 'env']),
    ['Encounters', arr('JsEncounter', s.encounters.map((e) => obj('JsEncounter', [
      ['X', num(e.x)], ['Gate', num(e.gate)], ['MimicIntro', scalar(e.mimicIntro)],
      ['Waves', `new string[][] { ${e.waves.map(strArray).join(', ')} }`],
    ], level + 2)), level + 1)],
    ['Tutorial', s.tutorial ? arr('JsTutorialPrompt', s.tutorial.map((t) => obj('JsTutorialPrompt', [['Step', str(t.step)], ['At', num(t.at)]], level + 2)), level + 1) : undefined],
    ['Clues', s.clues && s.clues.length ? arr('JsClue', s.clues.map((c) => obj('JsClue', [['Id', str(c.id)], ['X', num(c.x)], ['Label', str(c.label)], ['Scene', scalar(c.scene)]], level + 2)), level + 1) : undefined],
  ], level);
}
function stageRow(st, level) {
  const sc = st.scenes;
  return obj('JsStage', [
    ...pick(st, ['id', 'kind', 'chapter', 'name', 'enemy', 'foe', 'foeTitle', 'format', 'env', 'pos']),
    ['Reward', rewardObj(st.reward, level + 1)],
    ['SceneBefore', sc && sc.before ? str(sc.before) : undefined], ['SceneAfter', sc && sc.after ? str(sc.after) : undefined], ['SceneDefeat', sc && sc.defeat ? str(sc.defeat) : undefined],
    ['Stretch', stretchObj(st.stretch, level + 1)],
  ], level);
}

/* ---------------- gather ---------------- */
const chapters = CHAPTERS.map((id) => {
  const ch = js.CAMPAIGN.chapters.find((c) => c.id === id);
  if (!ch) throw new Error('chapter not found in the JS campaign: ' + id);
  return ch;
});
const enemyIds = [];
const addEnemy = (id) => { if (id && !enemyIds.includes(id)) enemyIds.push(id); };
for (const ch of chapters) for (const st of ch.stages) {
  addEnemy(st.enemy);
  if (st.stretch) for (const e of st.stretch.encounters) for (const wave of e.waves) wave.forEach(addEnemy);
}
for (const id of enemyIds) if (!js.ENEMIES[id]) throw new Error('stage names a missing enemy: ' + id);

function gitInfo() {
  const run = (...a) => cp.execFileSync('git', ['-C', JS_DIR, ...a], { encoding: 'utf8' }).trim();
  try {
    const hash = run('rev-parse', 'HEAD'), date = run('log', '-1', '--format=%cI');
    const dirty = run('status', '--porcelain', '--', 'src', 'tests/helpers').length > 0;
    return `git ${hash} (${date})${dirty ? ' + uncommitted changes in src/' : ''}`;
  } catch (e) {
    const m = fs.statSync(path.join(JS_DIR, 'src', 'js', '10-data.js')).mtime.toISOString();
    return `no git repository; src/js/10-data.js modified ${m}`;
  }
}

/* ---------------- write ---------------- */
const L = 2; // indentation level of members inside the class inside the namespace
const out = [];
out.push(`// <auto-generated>
// Generated by Tools/js_export/export.js from the JS game: ${gitInfo()}
// Do not edit by hand. To change a JS value, change the JS game and re-run: node Tools/js_export/export.js (ADR-0010).
// Values are read after every JS module has loaded (load-sim), so later overrides are included.
// </auto-generated>
using System.Collections.Generic;

namespace Kage.Rules
{
    public static partial class JsTables
    {
        public const int GearMaxWeapons = ${js.GEAR_MAX.weapons};
        public const int GearMaxHelmets = ${js.GEAR_MAX.helmets};
        public const int GearMaxArmors = ${js.GEAR_MAX.armors};
        public const int DevCoins = ${js.DEV.coins};
        public const int DevGems = ${js.DEV.gems};
`);
const member = (type, name, rows) => out.push(`        public static readonly ${type}[] ${name} = ${arr(type, rows, L)};\n`);
member('JsTier', 'GearTiers', Object.entries(js.GEAR_TIERS).map(([id, t]) => obj('JsTier', [['Id', str(id)], ['Rank', num(t.rank)], ['Label', str(t.label)]], L + 1)));
member('JsWeapon', 'Weapons', Object.entries(js.WEAPONS).map(([id, w]) => weaponRow(id, w, L + 1)));
member('JsGear', 'Helmets', Object.entries(js.HELMETS).map(([id, g]) => gearRow(id, g, L + 1)));
member('JsGear', 'Armors', Object.entries(js.ARMORS).map(([id, g]) => gearRow(id, g, L + 1)));
member('JsEnemy', 'Enemies', enemyIds.map((id) => enemyRow(id, js.ENEMIES[id], L + 1)));
member('JsAiProfile', 'AiProfiles', Object.entries(js.AI_PROFILES).map(([id, a]) => aiRow(id, a, L + 1)));
member('JsTutorialStep', 'TutorialSteps', Object.entries(js.TUTORIAL_STEPS).map(([id, t]) => obj('JsTutorialStep', [['Id', str(id)], ['Verb', str(t.verb)], ['Hint', str(t.hint)]], L + 1)));
member('JsChapter', 'Chapters', chapters.map((ch) => obj('JsChapter', [
  ...pick(ch, ['id', 'name', 'title', 'place', 'env', 'diff']),
  ['Stages', arr('JsStage', ch.stages.map((st) => stageRow(st, L + 2)), L + 1)],
], L + 1)));
out.push('    }\n}\n');

const text = out.join('\n').replace(/\r?\n/g, '\n');
if (CHECK) {
  const have = fs.existsSync(OUT) ? fs.readFileSync(OUT, 'utf8').replace(/\r\n/g, '\n') : null;
  if (have !== text) { console.error('JsTables.g.cs is out of date; run: node Tools/js_export/export.js'); process.exit(1); }
  console.log('JsTables.g.cs is up to date');
} else {
  fs.mkdirSync(path.dirname(OUT), { recursive: true });
  fs.writeFileSync(OUT, text, 'utf8');
  console.log(`wrote ${path.relative(REPO_ROOT, OUT)}: ${Object.keys(js.WEAPONS).length} weapons, ${Object.keys(js.HELMETS).length} helmets, ${Object.keys(js.ARMORS).length} armours, ${enemyIds.length} enemies, ${chapters.length} chapters`);
}
