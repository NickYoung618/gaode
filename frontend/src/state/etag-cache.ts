export type Snapshot = { revision?: number; observedRevision?: number; persistedRevision?: number; [key: string]: unknown };
export class EtagCache<T extends Snapshot> {
  private value?: T; private etag?: string;
  get current() { return this.value; }
  get tag() { return this.etag; }
  accept(value: T | undefined, etag?: string, status = 200): boolean {
    if (status === 304) return Boolean(this.value);
    if (!value) return false;
    const incoming = Number(value.observedRevision ?? value.revision ?? value.persistedRevision ?? 0);
    const current = Number(this.value?.observedRevision ?? this.value?.revision ?? this.value?.persistedRevision ?? 0);
    if (this.value && incoming < current) return false;
    this.value = structuredClone(value); if (etag) this.etag = etag; return true;
  }
}
