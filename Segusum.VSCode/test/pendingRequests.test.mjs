import assert from 'node:assert/strict';
import { PendingRequestRegistry } from '../out/pendingRequests.js';

const timeout = (promise, ms = 250) => Promise.race([
  promise,
  new Promise((_, reject) => setTimeout(() => reject(new Error('timeout')), ms)),
]);

for (let iteration = 0; iteration < 50; iteration++) {
  const registry = new PendingRequestRegistry();
  let firstSettled = false;
  let secondValue;
  const first = new Promise((resolve, reject) => registry.add(1, { resolve: () => { firstSettled = true; resolve(); }, reject }));
  const second = new Promise((resolve, reject) => registry.add(2, { resolve, reject }));
  assert.equal(registry.size, 2);
  assert.equal(registry.reject(1, new Error('cancelled')), true);
  await assert.rejects(first, /cancelled/);
  assert.equal(firstSettled, false);
  assert.equal(registry.size, 1);
  assert.equal(registry.resolve(1, 'late'), false);
  registry.resolve(2, 'latest');
  secondValue = await timeout(second);
  assert.equal(secondValue, 'latest');
  assert.equal(registry.size, 0);
  assert.equal(registry.reject(1, new Error('duplicate cancel')), false);
}

{
  const registry = new PendingRequestRegistry();
  const token = { isCancellationRequested: true };
  let normalRequestSent = false;
  let rejected = false;
  const request = new Promise((resolve, reject) => {
    if (token.isCancellationRequested) {
      rejected = true;
      reject(new Error('Request cancelled'));
      return;
    }
    registry.add(3, { resolve, reject });
    normalRequestSent = true;
  });
  await assert.rejects(request, /cancelled/);
  assert.equal(rejected, true);
  assert.equal(normalRequestSent, false);
  assert.equal(registry.size, 0);
}

{
  const registry = new PendingRequestRegistry();
  let lateResponseAccepted = false;
  let rejectRequest;
  const request = new Promise((resolve, reject) => {
    rejectRequest = reject;
    registry.add(4, { resolve: value => { lateResponseAccepted = value === 'late'; resolve(value); }, reject });
  });
  assert.equal(registry.reject(4, new Error('cancelled immediately after send')), true);
  await assert.rejects(request, /cancelled immediately/);
  assert.equal(registry.resolve(4, 'late'), false);
  assert.equal(lateResponseAccepted, false);
  assert.equal(registry.size, 0);
  void rejectRequest;
}

console.log('pending request cancellation stress: 50/50 passed; pre-cancel and late-response cases passed; pending=0');
