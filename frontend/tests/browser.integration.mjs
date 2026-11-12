// Explicit local browser checks; no packages, database writes or existing browser profiles.
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { once } from 'node:events';
import { runFinancialBrowserChecks } from './financial.browser.mjs';

const url = process.env.FRONTEND_URL || 'http://127.0.0.1:5173';
assert(['127.0.0.1', 'localhost'].includes(new URL(url).hostname), 'Only a local frontend is allowed');
const cache = resolve('node_modules/.cache/urbansketch-browser');
mkdirSync(cache, { recursive: true });
const profile = mkdtempSync(resolve(cache, 'profile-'));
const binary = process.env.BROWSER_BINARY || 'C:/Program Files/Google/Chrome/Application/chrome.exe';
if (process.argv.includes('--unavailable')) {
  const result = spawnSync(binary, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--dump-dom', '--virtual-time-budget=3000', `--user-data-dir=${profile}`, url],
  { windowsHide: true, encoding: 'utf8', timeout: 20000 });
  assert.equal(result.status, 0, 'Headless browser must complete');
  assert(result.stdout.includes('role="alert"') && result.stdout.includes('Повторить загрузку проектов'));
  assert(!result.stdout.includes('data-tep=') && !result.stdout.includes('Загрузка проектов…'));
  console.log('PASS: actual unavailable backend through Vite proxy displays an error and retry, without stale data or endless loading.');
  process.exit(0);
}
const browser = spawn(binary, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
  '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'], { windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'] });
const exited = once(browser, 'exit');
let socket;
let passed = 0;
const ownedModels = new Set();
try {
  const debuggingUrl = await new Promise((accept, reject) => {
    let log = '';
    const timeout = setTimeout(() => reject(new Error('Browser startup timed out')), 15000);
    browser.on('error', reject);
    browser.stderr.on('data', chunk => {
      log += chunk.toString();
      const match = log.match(/DevTools listening on (ws:\/\/[^\s]+)/);
      if (match) { clearTimeout(timeout); accept(match[1]); }
    });
  });
  const port = new URL(debuggingUrl).port;
  const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
  socket = new WebSocket(targets.find(target => target.type === 'page').webSocketDebuggerUrl);
  await once(socket, 'open');
  let serial = 0;
  const pending = new Map();
  const uncaught = [];
  const dialogs = [];
  let acceptDialogs = true;
  socket.addEventListener('message', event => {
    const message = JSON.parse(event.data);
    if (message.method === 'Runtime.exceptionThrown') uncaught.push(message.params.exceptionDetails.text);
    if (message.method === 'Page.javascriptDialogOpening') {
      dialogs.push(message.params);
      void cdp('Page.handleJavaScriptDialog', { accept: acceptDialogs });
    }
    if (message.method === 'Network.responseReceived' && message.params.response.status === 201 && message.params.response.url.endsWith('/api/financial-models')) {
      const headers = message.params.response.headers;
      const location = headers.Location || headers.location || '';
      const id = location.match(/\/financial-models\/([\da-f-]{36})$/i)?.[1];
      if (id) ownedModels.add(id);
    }
    const promise = pending.get(message.id);
    if (promise) { pending.delete(message.id); message.error ? promise.reject(new Error(message.error.message)) : promise.accept(message.result); }
  });
  const cdp = (method, params = {}) => new Promise((accept, reject) => {
    const id = ++serial;
    pending.set(id, { accept, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
  const evaluate = async expression => {
    const result = await cdp('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true, userGesture: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
    return result.result.value;
  };
  const waitFor = async expression => {
    for (let i = 0; i < 200; i++) {
      if (await evaluate(`Boolean(${expression})`)) return;
      await new Promise(accept => setTimeout(accept, 25));
    }
    throw new Error('Timed out waiting for: ' + expression);
  };
  const test = async (name, action) => { await action(); passed++; console.log('PASS ' + name); };
  const textIncludes = text => `document.body?.innerText.includes(${JSON.stringify(text)})`;
  const metric = (key, value) => `document.querySelector('[data-tep="${key}"] p:last-child')?.textContent.replace(/\\s/g,'') === ${JSON.stringify(value.replace(/\s/g, ''))}`;
  const select = async (id, value) => evaluate(`(() => {const element=document.getElementById(${JSON.stringify(id)});element.value=${JSON.stringify(value)};element.dispatchEvent(new Event('change',{bubbles:true}));})()`);
  const clickText = async text => evaluate(`Array.from(document.querySelectorAll('button')).find(button => button.textContent.includes(${JSON.stringify(text)})).click()`);
  const fixture = async path => {
    const response = await fetch(url + path, { signal: AbortSignal.timeout(8000) });
    assert.equal(response.status, 200, 'Real local API must be available before browser tests');
    return response.json();
  };
  const projects = await fixture('/api/projects');
  assert.equal(projects.length, 1, 'Real proxy must return the demo project');
  const project = projects[0];
  const variants = await fixture(`/api/projects/${project.id}/variants`);
  assert.equal(variants.length, 2);
  const details = await Promise.all(variants.map(variant => fixture(`/api/concepts/${variant.id}`)));
  await cdp('Page.enable');
  await cdp('Runtime.enable');
  await cdp('Network.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  let injection;
  async function load(mode) {
    if (injection) await cdp('Page.removeScriptToEvaluateOnNewDocument', { identifier: injection });
    const result = await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(${mockSetup.toString()})(${JSON.stringify({ mode, project, variants, details })});` });
    injection = result.identifier;
    await cdp('Page.navigate', { url: url + '/?browser-check=' + mode });
    await waitFor(`document.querySelector('#project-select') && window.__apiTest?.mode === ${JSON.stringify(mode)}`);
  }
  await test('real API: automatic project selection, A and all eleven metrics', async () => {
    await load('real');
    await waitFor(metric('residentialArea', '1000м²'));
    assert.equal(await evaluate(`document.querySelectorAll('[data-tep]').length`), 11);
    assert.equal(await evaluate(`document.getElementById('project-select').value`), project.id);
    assert.equal(await evaluate(`document.querySelectorAll('[data-tep] p:last-child').length`), 11);
    assert.equal(await evaluate(`Array.from(document.querySelectorAll('[data-tep] p:last-child')).filter(p=>p.textContent==='Нет данных').length`), 8);
    writeFileSync(resolve(cache, 'desktop.png'), Buffer.from((await cdp('Page.captureScreenshot', { format: 'png' })).data, 'base64'));
  });
  await test('real API: concept B values and persistent selection across all screens', async () => {
    await select('concept-select', variants[1].id);
    await waitFor(metric('residentialArea', '900м²'));
    assert(await evaluate(metric('commercialArea', '300м²')));
    assert(await evaluate(metric('parkingSpaces', '25шт.')));
    for (const name of ['Финансовые параметры', 'Результаты', 'Сравнение концепций', 'Исходные данные']) {
      await clickText(name);
      await waitFor(`document.querySelector('h1')?.textContent === ${JSON.stringify(name)}`);
    }
    assert.equal(await evaluate(`document.getElementById('concept-select').value`), variants[1].id);
    assert(await evaluate(metric('residentialArea', '900м²')));
    assert(await evaluate(`window.__apiRequests.every(r=>r.method==='GET')`));
  });
  await test('mobile layout has no horizontal overflow', async () => {
    await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true });
    assert(await evaluate('document.documentElement.scrollWidth <= window.innerWidth'));
    writeFileSync(resolve(cache, 'mobile.png'), Buffer.from((await cdp('Page.captureScreenshot', { format: 'png' })).data, 'base64'));
    await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  });
  for (const [mode, message] of [['projects-loading', 'Загрузка проектов'], ['variants-loading', 'Загрузка концепций'], ['details-loading', 'Загрузка ТЭП']])
    await test(mode + ' finishes with data', async () => { await load(mode); await waitFor(textIncludes(message)); await waitFor(metric('residentialArea', '1000м²')); });
  await test('empty projects and empty variants show distinct empty states', async () => {
    await load('empty-projects'); await waitFor(textIncludes('В базе пока нет проектов'));
    assert.equal(await evaluate('document.querySelectorAll("[data-tep]").length'), 0);
    await load('empty-variants'); await waitFor(textIncludes('У выбранного проекта пока нет концепций'));
    assert(await evaluate(`!window.__apiRequests.some(r=>r.path.startsWith('/api/concepts/'))`));
  });
  for (const [mode, retry] of [['projects-error', 'проектов'], ['variants-error', 'концепций'], ['details-error', 'ТЭП']])
    await test(mode + ' stops loading and supports retry', async () => {
      await load(mode); await waitFor('document.querySelector("[role=alert]")');
      assert(!await evaluate(textIncludes('PRIVATE_SERVER_DETAIL')));
      assert.equal(await evaluate('document.querySelectorAll("[data-tep]").length'), 0);
      await evaluate('window.__apiTest.recovered=true');
      await clickText('Повторить загрузку ' + retry);
      await waitFor(metric('residentialArea', '1000м²'));
    });
  for (const mode of ['network-error', 'invalid-response']) await test(mode + ' is visible without endless loading', async () => {
    await load(mode); await waitFor('document.querySelector("[role=alert]")');
    assert.equal(await evaluate('document.querySelectorAll("[data-tep]").length'), 0);
    assert(!await evaluate(textIncludes('Загрузка проектов')));
  });
  await test('rapid switching hides previous data and ignores late responses even without transport abort', async () => {
    await load('race'); await waitFor(metric('residentialArea', '1000м²'));
    await select('concept-select', variants[1].id);
    await waitFor(textIncludes('Загрузка ТЭП'));
    assert.equal(await evaluate('document.querySelectorAll("[data-tep]").length'), 0);
    await select('concept-select', variants[0].id);
    await waitFor(metric('residentialArea', '1000м²'));
    await new Promise(accept => setTimeout(accept, 450));
    assert(await evaluate(metric('residentialArea', '1000м²')));
    await evaluate(`(() => { const e=document.getElementById('concept-select');for(const id of ${JSON.stringify([variants[1].id, variants[0].id, variants[1].id])}){e.value=id;e.dispatchEvent(new Event('change',{bubbles:true}));}})()`);
    await waitFor(metric('residentialArea', '900м²'));
  });
  await test('multiple projects remain selectable and changing project clears dependent data', async () => {
    await load('multiple-projects'); await waitFor('document.getElementById("project-select").options.length===3');
    assert.equal(await evaluate('document.getElementById("project-select").value'), '');
    await select('project-select', project.id); await waitFor(metric('residentialArea', '1000м²'));
    await select('project-select', 'a18d9b8a-3d42-4b92-b7e0-095b3e1a0200');
    await waitFor(metric('residentialArea', '42м²'));
    assert.equal(await evaluate('document.getElementById("concept-select").value'), 'a18d9b8a-3d42-4b92-b7e0-095b3e1a0201');
    await select('project-select', project.id); await waitFor(metric('residentialArea', '1000м²'));
  });
  await test('real zero is displayed as zero while unknown values stay unknown', async () => {
    await load('zeroes'); await waitFor(metric('residentsCount', '0чел.'));
    assert(await evaluate(metric('constructionResidentialArea', '0м²')));
    assert(await evaluate(metric('schoolPlaces', '0мест')));
    assert(await evaluate(`document.querySelector('[data-tep="preschoolPlaces"] p:last-child').textContent==='Нет данных'`));
    assert(await evaluate(`!window.__apiRequests.some(r=>r.path==='/api/projects/' || r.path==='/api/concepts/' || r.path.includes('//'))`));
  });
  await runFinancialBrowserChecks({ cdp, evaluate, waitFor, test, select, clickText, load, project, variants, url,
    dialogs, answerDialogs: value => { acceptDialogs = value; }, ownedModels, cache });
  assert.deepEqual(uncaught, [], 'No uncaught rendering errors');
  console.log(`Browser checks: ${passed}; failures: 0`);
  await cdp('Browser.close');
} finally {
  const remaining = [];
  for (const id of ownedModels) {
    try {
      const response = await fetch(`${url}/api/financial-models/${id}`, { signal: AbortSignal.timeout(8000) });
      if (response.status === 404) continue;
      if (response.status !== 200 || (await fetch(`${url}/api/financial-models/${id}`, { method: 'DELETE', signal: AbortSignal.timeout(8000) })).status !== 204) remaining.push(id);
    } catch { remaining.push(id); }
  }
  // Browser.close shuts down only this fresh browser, never a user's browser session.
  if (socket?.readyState === WebSocket.OPEN) {
    try { socket.send(JSON.stringify({ id: 999999, method: 'Browser.close' })); } catch { /* already closing */ }
  }
  await Promise.race([exited, new Promise(accept => setTimeout(accept, 3000))]);
  socket?.close();
  if (remaining.length) throw new Error('Known temporary models requiring cleanup: ' + remaining.join(', '));
}

function mockSetup({ mode, project, variants, details }) {
  const original = window.fetch.bind(window);
  window.__apiTest = { mode, recovered: false, counts: {} };
  window.__apiRequests = [];
  const otherProject = { ...project, id: 'a18d9b8a-3d42-4b92-b7e0-095b3e1a0200', name: 'Другой проект из API' };
  const otherDetail = { ...details[0], id: 'a18d9b8a-3d42-4b92-b7e0-095b3e1a0201', projectId: otherProject.id, name: 'Другая концепция из API', residentialArea: 42 };
  window.fetch = async (input, options = {}) => {
    const path = new URL(typeof input === 'string' ? input : input.url, location.href).pathname;
    if (!path.startsWith('/api/')) return original(input, options);
    window.__apiRequests.push({ path, method: options.method || 'GET' });
    if (mode === 'real') return original(input, options);
    const state = window.__apiTest;
    state.counts[path] = (state.counts[path] || 0) + 1;
    const isProjects = path === '/api/projects';
    const isVariants = path.endsWith('/variants');
    const isDetails = path.startsWith('/api/concepts/');
    if (mode === 'network-error') throw new TypeError('PRIVATE_NETWORK_DETAIL');
    if (!state.recovered && ((mode === 'projects-error' && isProjects) || (mode === 'variants-error' && isVariants) || (mode === 'details-error' && isDetails)))
      return Response.json({ detail: 'PRIVATE_SERVER_DETAIL' }, { status: mode === 'details-error' ? 404 : 500 });
    if (mode === 'invalid-response') return Response.json({ invalid: true });
    let data = isProjects ? (mode === 'empty-projects' ? [] : mode === 'multiple-projects' ? [project, otherProject] : [project])
      : isVariants ? (mode === 'empty-variants' ? [] : path.includes(otherProject.id) ? [otherDetail] : variants)
      : details.find(item => path.endsWith(item.id)) || otherDetail;
    if (mode === 'zeroes' && isDetails) data = { ...data, residentsCount: 0, schoolPlaces: 0, constructionResidentialArea: 0 };
    let delay = (mode === 'projects-loading' && isProjects) || (mode === 'variants-loading' && isVariants) || (mode === 'details-loading' && isDetails) ? 400 : 0;
    if (mode === 'race' && isDetails) delay = path.endsWith(variants[1].id) ? 300 : state.counts[path] === 1 ? 0 : 60;
    if (delay) await new Promise(accept => setTimeout(accept, delay)); // Deliberately ignores AbortSignal to test stale-response guards.
    return Response.json(data);
  };
}
