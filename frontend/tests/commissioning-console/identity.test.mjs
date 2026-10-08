import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const sandbox = { module: { exports: {} } };
vm.runInNewContext(readFileSync(new URL('../../src/commissioning-console.js', import.meta.url), 'utf8'), sandbox);
const { confirmIdentity, loginMatches, bindLogin } = sandbox.module.exports;
const profile = { profileId: 'offline-operator', expectedSubjectId: 'offline:operator', expectedRole: 'Operator' };
const identity = { schemaVersion: 'station01-identity/1', mode: 'RealDeviceCommissioning', purpose: 'Commissioning',
  authenticationSource: 'PreconfiguredCommissioning', profileId: profile.profileId, subjectId: profile.expectedSubjectId,
  role: 'Operator', displayName: 'OFFLINE Operator', permissions: ['Run.Read', 'Run.Start'] };
test('backend identity and selected profile must match, role clicks cannot grant authority', () => {
  const confirmed = confirmIdentity(identity, profile);
  assert.equal(loginMatches(confirmed, identity.displayName, 'L1'), true);
  assert.equal(loginMatches(confirmed, identity.displayName, 'L3'), false);
  assert.equal(loginMatches(confirmed, 'another person', 'L1'), false);
  assert.throws(() => confirmIdentity({ ...identity, role: 'ProcessEngineer' }, profile), /Mismatch/);
  assert.throws(() => confirmIdentity({ ...identity, purpose: 'Test' }, profile), /Mismatch/);
  assert.throws(() => confirmIdentity({ ...identity, subjectId: 'test:operator' }, profile), /Mismatch/);
});
test('existing login form rejects mismatch and navigates without username, role or credentials', () => {
  let selected = 'L3', destination = null, validity = '';
  const input = { value: '', addEventListener() {}, setCustomValidity(v) { validity = v; }, reportValidity() {} };
  const chips = ['L1', 'L2', 'L3'].map(role => ({ dataset: { role }, classList: { toggle(_, active) { if (active) selected = role; } } }));
  const document = { getElementById: () => input, querySelectorAll: () => chips, querySelector: () => ({ dataset: { role: selected } }) };
  const submit = bindLogin(document, identity, url => { destination = url; });
  assert.equal(input.value, identity.displayName); assert.equal(selected, 'L1');
  selected = 'L3'; assert.equal(submit({ preventDefault() {} }), false); assert.ok(validity); assert.equal(destination, null);
  selected = 'L1'; assert.equal(submit({ preventDefault() {} }), true); assert.equal(destination, 'prototype.html');
});
