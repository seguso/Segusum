"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.InvalidationScheduler = void 0;
exports.isGeneratedPath = isGeneratedPath;
const path = __importStar(require("path"));
const generatedDirectories = new Set(['bin', 'obj', 'node_modules', '.git']);
function isGeneratedPath(filePath) {
    const normalized = path.normalize(filePath).replace(/\\/g, '/');
    return normalized.split('/').some(part => generatedDirectories.has(part.toLowerCase()));
}
/** Coalesces source changes and permits only one invalidate RPC at a time. */
class InvalidationScheduler {
    options;
    timer;
    requested = false;
    inFlight = false;
    disposed = false;
    delayMs;
    constructor(options) {
        this.options = options;
        this.delayMs = options.delayMs ?? 200;
    }
    get isInFlight() { return this.inFlight; }
    get isScheduled() { return this.timer !== undefined; }
    request() {
        if (this.disposed)
            return;
        this.requested = true;
        if (this.inFlight) {
            this.options.log?.('invalidate coalesced (request in flight)');
            return;
        }
        if (this.timer)
            clearTimeout(this.timer);
        this.timer = setTimeout(() => { this.timer = undefined; void this.flush(); }, this.delayMs);
        this.options.log?.('invalidate scheduled');
    }
    dispose() {
        this.disposed = true;
        if (this.timer)
            clearTimeout(this.timer);
        this.timer = undefined;
        this.requested = false;
    }
    async flush() {
        if (this.disposed || this.inFlight || !this.requested)
            return;
        this.requested = false;
        this.inFlight = true;
        this.options.log?.('invalidate start');
        try {
            await this.options.send();
        }
        catch (error) {
            this.options.log?.(`invalidate failed: ${error}`);
        }
        finally {
            this.inFlight = false;
            this.options.log?.('invalidate end');
            if (!this.disposed && this.requested)
                this.request();
        }
    }
}
exports.InvalidationScheduler = InvalidationScheduler;
//# sourceMappingURL=invalidation.js.map