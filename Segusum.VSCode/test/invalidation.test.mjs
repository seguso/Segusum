import assert from 'node:assert/strict';
import { InvalidationScheduler, isGeneratedPath } from '../out/invalidation.js';

assert.equal(isGeneratedPath('C:/work/obj/Debug/generated.cs'), true);
assert.equal(isGeneratedPath('C:/work/bin/Release/generated.cs'), true);
assert.equal(isGeneratedPath('C:/work/node_modules/pkg/index.js'), true);
assert.equal(isGeneratedPath('C:/work/.git/index'), true);
assert.equal(isGeneratedPath('C:/work/src/ActionHandlers.seg'), false);

const wait = ms => new Promise(resolve => setTimeout(resolve, ms));

{
  let sends = 0;
  const scheduler = new InvalidationScheduler({ delayMs: 5, send: async () => { sends++; } });
  for (let i = 0; i < 500; i++) scheduler.request();
  await wait(30);
  assert.equal(sends, 1);
  scheduler.dispose();
}

{
  let sends = 0;
  let release;
  const first = new Promise(resolve => { release = resolve; });
  const scheduler = new InvalidationScheduler({ delayMs: 1, send: async () => { sends++; await first; } });
  scheduler.request();
  await wait(10);
  assert.equal(scheduler.isInFlight, true);
  for (let i = 0; i < 500; i++) scheduler.request();
  assert.equal(sends, 1);
  release();
  await wait(15);
  assert.equal(sends, 2);
  assert.equal(scheduler.isInFlight, false);
  scheduler.dispose();
}

console.log('invalidation filter/coalescing stress: 500 events bounded; in-flight follow-up bounded');
