import { test, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { getProjects, getProject, getProjectVariants, getConceptDetails } from '../src/api/client.ts';
import { formatTep } from '../src/formatTep.ts';

const originalFetch = globalThis.fetch;
afterEach(() => { globalThis.fetch = originalFetch; });
const project = { id: 'a18d9b8a-3d42-4b92-b7e0-095b3e1a0100', name: 'Project from API', createdAt: '2026-11-11T08:21:40.797641Z' };
const summary = { id: 'a18d9b8a-3d42-4b92-b7e0-095b3e1a0101', projectId: project.id, name: 'Concept from API', residentialArea: 1000, commercialArea: 200, parkingSpaces: 20 };
const detail = { ...summary, constructionResidentialArea: null, constructionCommercialArea: null, residentsCount: null, buildingsCount: null, maxFloors: null, socialAreas: null, preschoolPlaces: null, schoolPlaces: null };
function respond(data, status = 200) { globalThis.fetch = async () => Response.json(data, { status }); }
async function expectError(action, kind, status) {
  await assert.rejects(action, error => error.kind === kind && (status === undefined || error.status === status));
}
for (const [name, path, result, call] of [
  ['projects', '/api/projects', [project], signal => getProjects(signal)],
  ['project', `/api/projects/${project.id}`, project, signal => getProject(project.id, signal)],
  ['variants', `/api/projects/${project.id}/variants`, [summary], signal => getProjectVariants(project.id, signal)],
  ['detail', `/api/concepts/${summary.id}`, detail, signal => getConceptDetails(summary.id, signal)],
]) test(`GET ${name} uses correct route, signal and DTO`, async () => {
  const controller = new AbortController();
  globalThis.fetch = async (url, options) => {
    assert.equal(url, path);
    assert.equal(options.method, 'GET');
    assert.equal(options.signal, controller.signal);
    return Response.json(result);
  };
  assert.deepEqual(await call(controller.signal), result);
});
test('signal is forwarded to fetch', async () => {
  const controller = new AbortController();
  globalThis.fetch = async (_url, options) => { assert.equal(options.signal, controller.signal); return Response.json([project]); };
  await getProjects(controller.signal);
});
test('empty project and variant lists are valid', async () => {
  respond([]);
  assert.deepEqual(await getProjects(), []);
  assert.deepEqual(await getProjectVariants(project.id), []);
});
for (const status of [404, 500, 502]) test(`HTTP ${status} is not an empty success`, async () => {
  respond({ detail: 'PRIVATE_SERVER_DETAIL' }, status);
  await expectError(() => getProjects(), 'http', status);
});
test('network error has a safe message', async () => {
  globalThis.fetch = async () => { throw new Error('PRIVATE_NETWORK_DETAIL'); };
  await assert.rejects(() => getProjects(), error => error.kind === 'network' && !error.message.includes('PRIVATE_NETWORK_DETAIL'));
});
test('abort is propagated instead of a user-facing network error', async () => {
  globalThis.fetch = async () => { throw new DOMException('aborted', 'AbortError'); };
  await assert.rejects(() => getProjects(), error => error.name === 'AbortError');
});
test('HTML and broken JSON are invalid responses', async () => {
  globalThis.fetch = async () => new Response('<html>error</html>', { headers: { 'Content-Type': 'text/html' } });
  await expectError(() => getProjects(), 'invalid-response');
  globalThis.fetch = async () => new Response('{broken', { headers: { 'Content-Type': 'application/json' } });
  await expectError(() => getProjects(), 'invalid-response');
});
for (const [name, value] of [
  ['object instead of list', project], ['bad Guid', [{ ...project, id: 'bad' }]],
  ['missing UTC date', [{ id: project.id, name: project.name }]], ['invalid date', [{ ...project, createdAt: 'not-a-date' }]],
  ['duplicate IDs', [project, project]], ['null name', [{ ...project, name: null }]],
]) test(`invalid project response: ${name}`, async () => { respond(value); await expectError(() => getProjects(), 'invalid-response'); });
test('variant from a different project is rejected', async () => {
  respond([{ ...summary, projectId: summary.id }]);
  await expectError(() => getProjectVariants(project.id), 'invalid-response');
});
for (const [name, value] of [
  ['missing optional field', { ...detail, residentsCount: undefined }],
  ['wrong optional type', { ...detail, schoolPlaces: 'unknown' }],
  ['negative count', { ...detail, parkingSpaces: -1 }],
  ['wrong numeric type', { ...detail, residentialArea: '1000' }],
]) test(`invalid concept response: ${name}`, async () => { respond(value); await expectError(() => getConceptDetails(summary.id), 'invalid-response'); });
test('nulls and real zeroes remain distinct in detail', async () => {
  respond({ ...detail, buildingsCount: 0, socialAreas: 0 });
  const value = await getConceptDetails(summary.id);
  assert.equal(value.buildingsCount, 0);
  assert.equal(value.socialAreas, 0);
  assert.equal(value.constructionResidentialArea, null);
});
test('mismatched requested identity is rejected', async () => {
  respond({ ...project, id: summary.id });
  await expectError(() => getProject(project.id), 'invalid-response');
  respond({ ...detail, id: project.id });
  await expectError(() => getConceptDetails(summary.id), 'invalid-response');
});
test('empty identifiers are rejected before fetch', async () => {
  globalThis.fetch = async () => { assert.fail('fetch must not be called'); };
  for (const call of [getProject, getProjectVariants, getConceptDetails])
    await expectError(async () => call(''), 'invalid-request');
});
test('TEP formatting preserves null, zero, precision and units', () => {
  assert.equal(formatTep(null, 'м²'), 'Нет данных');
  assert.equal(formatTep(0, 'шт.'), '0 шт.');
  assert.equal(formatTep(0, 'м²'), '0 м²');
  assert.equal(formatTep(1000, 'м²').replace(/\s/g, ' '), '1 000 м²');
  assert.equal(formatTep(1.25, 'м²'), '1,25 м²');
});
