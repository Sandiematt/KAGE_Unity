// Minimal Chrome DevTools Protocol driver for the JS game's UI (Microsoft Edge headless, no npm packages; Node 22+ has WebSocket).
// Used by capture.mjs to take screenshots and frame strips of the JS screens for the SAN-67 / SAN-90 side-by-side review.
import { spawn, execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';

const EDGE = ['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', 'C:/Program Files/Microsoft/Edge/Application/msedge.exe'].find(p => fs.existsSync(p));
export const sleep = ms => new Promise(r => setTimeout(r, ms));

/** Builds a private copy of the JS game (never touches its index.html) and returns the html path. */
export function buildGame(jsRoot, outHtml, patch) {
  fs.mkdirSync(path.dirname(outHtml), { recursive: true });
  execFileSync(process.execPath, [path.join(jsRoot, 'build.mjs'), '--out', outHtml], { stdio: 'ignore' });
  let html = fs.readFileSync(outHtml, 'utf8');
  if (patch) html = patch(html);
  fs.writeFileSync(outHtml, html);
  return outHtml;
}

export async function launch(htmlPath, { width = 1600, height = 900, port = 9400 + Math.floor(Math.random() * 400), init } = {}) {
  if (!EDGE) throw new Error('Microsoft Edge not found');
  const ud = path.join(os.tmpdir(), 'kage-cdp-' + process.pid + '-' + port);
  const proc = spawn(EDGE, ['--headless=new', '--user-data-dir=' + ud, '--no-first-run', '--enable-unsafe-swiftshader', '--use-angle=swiftshader', '--hide-scrollbars',
    '--remote-debugging-port=' + port, '--window-size=' + width + ',' + height, '--force-device-scale-factor=1', 'about:blank'], { stdio: 'ignore' });
  let targets;
  for (let i = 0; i < 80; i++) {
    try { targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json(); if (targets.some(t => t.type === 'page')) break; } catch {}
    await sleep(250);
  }
  const page = targets && targets.find(t => t.type === 'page');
  if (!page) { proc.kill(); throw new Error('no page target'); }
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });
  let id = 0; const pending = new Map(); const listeners = [];
  ws.onmessage = e => {
    const m = JSON.parse(e.data);
    if (m.id && pending.has(m.id)) { const { res, rej } = pending.get(m.id); pending.delete(m.id); m.error ? rej(new Error(m.error.message)) : res(m.result); }
    else if (m.method) for (const l of listeners) l(m);
  };
  const send = (method, params = {}) => new Promise((res, rej) => { const i = ++id; pending.set(i, { res, rej }); ws.send(JSON.stringify({ id: i, method, params })); });
  await send('Page.enable'); await send('Runtime.enable');
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
  if (init) await send('Page.addScriptToEvaluateOnNewDocument', { source: init });
  await send('Page.navigate', { url: 'file:///' + htmlPath.split(path.sep).join('/') });
  await sleep(1500);
  const api = {
    send,
    on: l => listeners.push(l),
    eval: async (expr) => {
      const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true });
      if (r.exceptionDetails) throw new Error('page: ' + (r.exceptionDetails.exception && r.exceptionDetails.exception.description || r.exceptionDetails.text));
      return r.result.value;
    },
    shot: async (file, clip) => {
      const r = await send('Page.captureScreenshot', { format: 'png', ...(clip ? { clip: { ...clip, scale: 1 } } : {}) });
      fs.mkdirSync(path.dirname(file), { recursive: true });
      fs.writeFileSync(file, Buffer.from(r.data, 'base64'));
      return file;
    },
    /** Runs `action` and records the screen for `ms`; returns [{t, data(base64 png)}] with t in ms since the action began. */
    record: async (action, ms) => {
      const frames = []; let t0 = 0;
      api.on(m => { if (m.method === 'Page.screencastFrame') { frames.push({ t: Date.now() - t0, data: m.params.data }); send('Page.screencastFrameAck', { sessionId: m.params.sessionId }).catch(() => {}); } });
      await send('Page.startScreencast', { format: 'png', everyNthFrame: 1 });
      t0 = Date.now(); await action(); await sleep(ms);
      await send('Page.stopScreencast');
      return frames;
    },
    close: async () => { try { ws.close(); } catch {} proc.kill(); await sleep(300); try { fs.rmSync(ud, { recursive: true, force: true }); } catch {} },
  };
  return api;
}
