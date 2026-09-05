import assert from 'node:assert/strict';
import fs from 'node:fs';
import { semanticDocumentSnapshot } from '../out/semanticRequest.js';

const host = fs.readFileSync(new URL('../../Segusum.Tooling.Host/Program.cs', import.meta.url), 'utf8');

// Read-only semantic queries must build their workspace from the same snapshot
// that supplied the editor coordinates. This guards the regression where the
// host deliberately erased HostParams.Text before Definition/References.
assert.match(host, /Workspace\(p, ct\)\.GetDefinition/);
assert.match(host, /Workspace\(p, ct\)\.FindReferencesAsync/);
assert.match(host, /var workspace = Workspace\(p, ct\);/);
assert.doesNotMatch(host, /p with \{ Text = null \}/);

const disk = 'world game\nuse oldSymbol for objective:\n';
const dirtyWithInsertedLines = 'world game\n\n\nuse newSymbol for objective:\n';
const dirtyWithRemovedLines = 'world game\nuse newSymbol for objective:';
const dirtyWithColumnChange = 'world game\nuse newSymbol for objective:   ';

const inserted = semanticDocumentSnapshot('C:/workspace/ActionHandlers.seg', 4, 8, dirtyWithInsertedLines);
const removed = semanticDocumentSnapshot(inserted.path, 2, 8, dirtyWithRemovedLines);
const columnChanged = semanticDocumentSnapshot(inserted.path, 2, 35, dirtyWithColumnChange);

assert.notEqual(inserted.text, disk);
assert.equal(inserted.text, dirtyWithInsertedLines);
assert.equal(removed.text, dirtyWithRemovedLines);
assert.equal(columnChanged.text, dirtyWithColumnChange);

console.log('host dirty overlay contract: definition, references and completion use the editor snapshot');
