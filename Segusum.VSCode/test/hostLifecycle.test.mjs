import assert from 'node:assert/strict';
import { getOrStartClient } from '../out/hostLifecycle.js';

const clients = new Map();
let createCount = 0;
let startCount = 0;
let initializeCount = 0;
let resolveStart;
let startTask;

const prewarmedClient = {
  start() {
    if (!startTask) {
      startCount++;
      startTask = new Promise(resolve => { resolveStart = resolve; });
      initializeCount++;
    }
    return startTask;
  },
};

const create = () => {
  createCount++;
  return prewarmedClient;
};

const prewarm = getOrStartClient(clients, 'project', create);
assert.equal(createCount, 1);
assert.equal(startCount, 1);
assert.equal(initializeCount, 1);

// This models an interactive request arriving while prewarm's initialize RPC
// is still pending. It must reuse the same client and await the same startTask.
const interactive = getOrStartClient(clients, 'project', () => {
  throw new Error('a second host must not be created');
});
assert.equal(createCount, 1);
assert.equal(startCount, 1);
assert.equal(initializeCount, 1);

resolveStart();
const [prewarmed, interactiveClient] = await Promise.all([prewarm, interactive]);
assert.equal(prewarmed, prewarmedClient);
assert.equal(interactiveClient, prewarmedClient);
assert.equal(clients.get('project'), prewarmedClient);

const failedClients = new Map();
let failureClient;
await assert.rejects(
  getOrStartClient(failedClients, 'failed', () => {
    failureClient = { start: async () => { throw new Error('initialize failed'); } };
    return failureClient;
  }),
  /initialize failed/,
);
assert.equal(failedClients.has('failed'), false);

console.log('host lifecycle race regression passed');
