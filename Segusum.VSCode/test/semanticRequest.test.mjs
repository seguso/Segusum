import assert from 'node:assert/strict';
import { semanticDocumentSnapshot } from '../out/semanticRequest.js';

const diskText = 'world game\nuse oldSymbol for objective:';
const dirtyText = 'world game\nuse newSymbol for objective:\n';
const snapshot = semanticDocumentSnapshot('C:/workspace/ActionHandlers.seg', 2, 8, dirtyText);

assert.equal(snapshot.path, 'C:/workspace/ActionHandlers.seg');
assert.equal(snapshot.text, dirtyText);
assert.notEqual(snapshot.text, diskText);
assert.deepEqual(snapshot, { path: 'C:/workspace/ActionHandlers.seg', line: 2, column: 8, text: dirtyText });

// Definition receives the same overlay contract independently of completion.
const definitionFirst = semanticDocumentSnapshot(snapshot.path, snapshot.line, snapshot.column, snapshot.text);
assert.deepEqual(definitionFirst, snapshot);
const completion = semanticDocumentSnapshot(snapshot.path, snapshot.line, snapshot.column, snapshot.text);
assert.deepEqual(completion, definitionFirst);

console.log('dirty-buffer semantic snapshot: definition-first and completion use identical editor text');
