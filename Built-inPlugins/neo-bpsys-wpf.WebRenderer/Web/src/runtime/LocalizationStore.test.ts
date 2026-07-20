import { describe, expect, it } from 'vitest'
import { LocalizationStore, isWebLocalizationSnapshot } from './LocalizationStore'

const snapshot = (revision: number, culture = 'en-US') => ({ SchemaVersion: 1, Revision: revision, Culture: culture, StaticTexts: { 'control:test:title': 'Title' }, MapV2Texts: {} })

describe('LocalizationStore', () => {
  it('atomically applies complete snapshots and rejects stale revisions', () => {
    const store = new LocalizationStore()
    expect(store.apply(snapshot(2))).toBe(true)
    expect(store.snapshot?.StaticTexts['control:test:title']).toBe('Title')
    expect(store.apply(snapshot(1, 'zh-CN'))).toBe(false)
    expect(store.snapshot?.Culture).toBe('en-US')
  })

  it('rejects unknown schema without exposing a key fallback', () => {
    const store = new LocalizationStore()
    expect(isWebLocalizationSnapshot({ SchemaVersion: 99, Revision: 1, Culture: 'en-US', StaticTexts: {}, MapV2Texts: {} })).toBe(false)
    expect(store.snapshot).toBeNull()
  })
})
