import test from 'node:test';import assert from 'node:assert/strict';import {readFileSync} from 'node:fs';import vm from 'node:vm';
const sandbox={module:{exports:{}}};vm.runInNewContext(readFileSync(new URL('../../src/commissioning-console.js',import.meta.url),'utf8'),sandbox);const api=sandbox.module.exports;
test('confirmed seven slots use business camera roles, unknown roles do not borrow a slot',()=>{
const items=[['Detection','C'],['Detection','D'],['Detection','A'],['Detection','B'],['E','E'],['ThreeD','ThreeD'],['F','F']];
assert.deepEqual(items.map(([role,businessCamera])=>api.mediaSlot({role,businessCamera})),[0,1,2,3,4,5,6]);assert.equal(api.mediaSlot({role:'Unknown',businessCamera:'C'}),undefined);
});
