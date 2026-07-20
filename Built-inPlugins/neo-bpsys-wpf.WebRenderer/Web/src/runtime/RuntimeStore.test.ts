import { beforeEach, describe, expect, it, vi } from 'vitest'
import { RuntimeStore } from './RuntimeStore'

beforeEach(() => {
  vi.stubGlobal('Image', class { src = ''; decode() { return Promise.resolve() } })
})

const asset = (revision: string) => ({ Kind: 'image' as const, Token: revision, Url: `/runtime-assets/${revision}`, ContentType: 'image/png', Revision: revision, NaturalWidthDip: 100, NaturalHeightDip: 200 })

describe('RuntimeStore', () => {
  it('retains resolved assets for pending/failed and clears explicit null', async () => {
    const store = new RuntimeStore()
    await store.enqueue('snapshot', { SchemaVersion: 2, Generation: 1, Sequence: 1, Values: { picture: { State: 'resolved', Kind: 'asset', Asset: asset('a') } } })
    await store.enqueue('bindingPatch', { SchemaVersion: 2, Generation: 1, Sequence: 2, Values: { picture: { State: 'pending', Kind: 'asset' } } })
    expect(store.state.values.picture).toEqual(asset('a'))
    await store.enqueue('bindingPatch', { SchemaVersion: 2, Generation: 1, Sequence: 3, Values: { picture: { State: 'failed', Kind: 'asset', Diagnostic: 'failed' } } })
    expect(store.state.values.picture).toEqual(asset('a'))
    await store.enqueue('bindingPatch', { SchemaVersion: 2, Generation: 1, Sequence: 4, Values: { picture: { State: 'null', Kind: 'null' } } })
    expect(store.state.values.picture).toBeNull()
  })

  it('atomically advances after decode and never reuses an old generation', async () => {
    const store = new RuntimeStore()
    await store.enqueue('snapshot', { SchemaVersion: 2, Generation: 1, Sequence: 1, Values: { picture: { Kind: 'asset', Asset: asset('a') } } })
    await store.enqueue('snapshot', { SchemaVersion: 2, Generation: 2, Sequence: 2, Values: { other: { Kind: 'string', Value: 'next' } } })
    expect(store.state.values.picture).toBeUndefined()
    expect(store.state.generation).toBe(2)
  })

  it('waits for a required sequence and fails open on a newer generation', async () => {
    const store = new RuntimeStore(); const waiting = store.waitFor(1, 2, 1000)
    await store.enqueue('snapshot', { SchemaVersion: 2, Generation: 1, Sequence: 2, Values: {} })
    await expect(waiting).resolves.toBe(true)
    await expect(store.waitFor(0, 99, 1000)).resolves.toBe(false)
  })
})
