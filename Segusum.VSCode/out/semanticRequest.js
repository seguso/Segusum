"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.semanticDocumentSnapshot = semanticDocumentSnapshot;
/** The text sent with every read-only semantic query is the editor snapshot. */
function semanticDocumentSnapshot(path, line, column, text) {
    return { path, line, column, text };
}
//# sourceMappingURL=semanticRequest.js.map