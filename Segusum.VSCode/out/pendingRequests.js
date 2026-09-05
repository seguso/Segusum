"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.PendingRequestRegistry = void 0;
class PendingRequestRegistry {
    entries = new Map();
    get size() { return this.entries.size; }
    has(id) { return this.entries.has(id); }
    add(id, entry) { this.entries.set(id, entry); }
    resolve(id, value) {
        const entry = this.entries.get(id);
        if (!entry)
            return false;
        this.entries.delete(id);
        entry.dispose?.();
        entry.resolve(value);
        return true;
    }
    reject(id, error) {
        const entry = this.entries.get(id);
        if (!entry)
            return false;
        this.entries.delete(id);
        entry.dispose?.();
        entry.reject(error);
        return true;
    }
    clear(error) {
        for (const id of this.entries.keys())
            this.reject(id, error);
    }
}
exports.PendingRequestRegistry = PendingRequestRegistry;
//# sourceMappingURL=pendingRequests.js.map