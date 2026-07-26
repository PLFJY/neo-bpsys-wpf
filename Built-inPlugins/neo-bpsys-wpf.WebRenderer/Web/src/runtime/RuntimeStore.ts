import { emptyRuntime, resolvedRuntimeValue, type RuntimeState, type WebRuntimeAsset, type WebRuntimeValue } from '../protocol/runtime'

type RuntimePayload = { SchemaVersion?: number; Generation?: number; Sequence?: number; LocalizationRevision?: number; Values?: Record<string, WebRuntimeValue> }
type Waiter = { generation: number; sequence: number; finish: (result: boolean) => void; timer: number }

const diagnosed = new Set<string>()

async function decodeAsset(asset: WebRuntimeAsset): Promise<void> {
  const image = new Image()
  image.src = asset.Url
  if (typeof image.decode === 'function') await image.decode()
  else await new Promise<void>((resolve, reject) => { image.onload = () => resolve(); image.onerror = () => reject(new Error('RuntimeAssetDecodeFailed')) })
}

export class RuntimeStore {
  state: RuntimeState = emptyRuntime()
  private listeners = new Set<() => void>()
  private waiters = new Set<Waiter>()
  private queue: Promise<void> = Promise.resolve()
  private decoded = new Map<string, Promise<void>>()

  subscribe(listener: () => void): () => void { this.listeners.add(listener); return () => this.listeners.delete(listener) }

  enqueue(type: 'snapshot' | 'bindingPatch', payload: RuntimePayload): Promise<void> {
    this.queue = this.queue.then(() => this.apply(type, payload)).catch(error => console.warn('[Web Renderer] runtime patch failed.', error))
    return this.queue
  }

  reset(): void {
    this.state = emptyRuntime()
    this.decoded.clear()
    this.resolveWaiters(false)
    this.notify()
  }

  waitFor(generation: number, sequence: number, timeoutMs: number): Promise<boolean> {
    if (this.state.generation === generation && this.state.sequence >= sequence) return Promise.resolve(true)
    if (this.state.generation > generation) return Promise.resolve(false)
    return new Promise<boolean>(finish => {
      const waiter: Waiter = { generation, sequence, finish, timer: window.setTimeout(() => { this.waiters.delete(waiter); finish(false) }, timeoutMs) }
      this.waiters.add(waiter)
    })
  }

  private async apply(type: 'snapshot' | 'bindingPatch', payload: RuntimePayload): Promise<void> {
    if ((payload.SchemaVersion ?? 1) > 2 || typeof payload.Sequence !== 'number') return
    const generation = payload.Generation ?? 0
    const previous = this.state
    if (type === 'snapshot') {
      if (generation < previous.generation || generation === previous.generation && payload.Sequence <= previous.sequence) return
    } else if (generation !== previous.generation || payload.Sequence <= previous.sequence) return

    const nextValues = type === 'snapshot' || generation !== previous.generation ? {} as Record<string, unknown> : { ...previous.values }
    const entries = Object.entries(payload.Values ?? {})
    await Promise.all(entries.map(async ([path, value]) => {
      const state = value.State ?? (value.Kind === 'null' ? 'null' : 'resolved')
      if (state === 'pending') { if (generation === previous.generation && path in previous.values) nextValues[path] = previous.values[path]; return }
      if (state === 'failed') {
        if (generation === previous.generation && path in previous.values) nextValues[path] = previous.values[path]
        const diagnostic = `${path}:${value.Diagnostic ?? 'RuntimeAssetFailed'}`
        if (!diagnosed.has(diagnostic)) { diagnosed.add(diagnostic); console.warn(`[Web Renderer] runtime asset retained after failure. path=${path} diagnostic=${value.Diagnostic ?? 'RuntimeAssetFailed'}`) }
        return
      }
      if (state === 'null' || value.Kind === 'null') { nextValues[path] = null; return }
      if (value.Kind === 'asset' && value.Asset) {
        let pending = this.decoded.get(value.Asset.Revision)
        if (!pending) { pending = decodeAsset(value.Asset); this.decoded.set(value.Asset.Revision, pending) }
        try { await pending; nextValues[path] = value.Asset }
        catch {
          this.decoded.delete(value.Asset.Revision)
          const diagnostic = `${path}:${value.Asset.Revision}`
          if (!diagnosed.has(diagnostic)) { diagnosed.add(diagnostic); console.warn(`[Web Renderer] runtime asset decode failed. path=${path}`) }
        }
        return
      }
      nextValues[path] = resolvedRuntimeValue(value)
    }))
    this.state = { values: nextValues, generation, sequence: payload.Sequence, localizationRevision: payload.LocalizationRevision ?? previous.localizationRevision }
    const revisions = new Set(Object.values(nextValues).map(value => value && typeof value === 'object' && 'Revision' in value ? String(value.Revision) : '').filter(Boolean))
    for (const revision of this.decoded.keys()) if (!revisions.has(revision)) this.decoded.delete(revision)
    this.notify()
    this.resolveWaiters(true)
  }

  private notify(): void { for (const listener of this.listeners) listener() }

  private resolveWaiters(applied: boolean): void {
    for (const waiter of [...this.waiters]) {
      const result = this.state.generation === waiter.generation && this.state.sequence >= waiter.sequence
      const obsolete = this.state.generation > waiter.generation
      if (result || obsolete || !applied) {
        clearTimeout(waiter.timer); this.waiters.delete(waiter); waiter.finish(result)
      }
    }
  }
}
