import * as path from 'path';

const generatedDirectories = new Set(['bin', 'obj', 'node_modules', '.git']);

export function isGeneratedPath(filePath: string): boolean {
  const normalized = path.normalize(filePath).replace(/\\/g, '/');
  return normalized.split('/').some(part => generatedDirectories.has(part.toLowerCase()));
}

export type InvalidationSchedulerOptions = {
  delayMs?: number;
  send: () => Promise<void>;
  log?: (message: string) => void;
};

/** Coalesces source changes and permits only one invalidate RPC at a time. */
export class InvalidationScheduler {
  private timer?: ReturnType<typeof setTimeout>;
  private requested = false;
  private inFlight = false;
  private disposed = false;
  private readonly delayMs: number;

  constructor(private readonly options: InvalidationSchedulerOptions) {
    this.delayMs = options.delayMs ?? 200;
  }

  get isInFlight(): boolean { return this.inFlight; }
  get isScheduled(): boolean { return this.timer !== undefined; }

  request(): void {
    if (this.disposed) return;
    this.requested = true;
    if (this.inFlight) {
      this.options.log?.('invalidate coalesced (request in flight)');
      return;
    }
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => { this.timer = undefined; void this.flush(); }, this.delayMs);
    this.options.log?.('invalidate scheduled');
  }

  dispose(): void {
    this.disposed = true;
    if (this.timer) clearTimeout(this.timer);
    this.timer = undefined;
    this.requested = false;
  }

  private async flush(): Promise<void> {
    if (this.disposed || this.inFlight || !this.requested) return;
    this.requested = false;
    this.inFlight = true;
    this.options.log?.('invalidate start');
    try {
      await this.options.send();
    } catch (error) {
      this.options.log?.(`invalidate failed: ${error}`);
    } finally {
      this.inFlight = false;
      this.options.log?.('invalidate end');
      if (!this.disposed && this.requested) this.request();
    }
  }
}
