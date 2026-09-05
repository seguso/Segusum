export type PendingRequest<T> = {
  resolve: (value: T) => void;
  reject: (error: unknown) => void;
  dispose?: () => void;
};

export class PendingRequestRegistry<T> {
  private readonly entries = new Map<number, PendingRequest<T>>();

  get size(): number { return this.entries.size; }

  add(id: number, entry: PendingRequest<T>): void { this.entries.set(id, entry); }

  resolve(id: number, value: T): boolean {
    const entry = this.entries.get(id);
    if (!entry) return false;
    this.entries.delete(id);
    entry.dispose?.();
    entry.resolve(value);
    return true;
  }

  reject(id: number, error: unknown): boolean {
    const entry = this.entries.get(id);
    if (!entry) return false;
    this.entries.delete(id);
    entry.dispose?.();
    entry.reject(error);
    return true;
  }

  clear(error: unknown): void {
    for (const id of this.entries.keys()) this.reject(id, error);
  }
}
