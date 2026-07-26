import { describe, expect, it } from 'vitest'
import { compare } from './WebBehaviorComparators'

describe('typed behavior payload comparisons', () => {
  it('compares arrays item by item', () => {
    expect(compare(['0', '1'], 'Contains', '0')).toBe(true)
    expect(compare([0, 1], 'Contains', '0')).toBe(true)
    expect(compare([0, 1], 'NotContains', '2')).toBe(true)
  })

  it('compares booleans and enum wire names semantically', () => {
    expect(compare(true, 'Equals', 'true')).toBe(true)
    expect(compare('PickSur', 'Equals', 'picksur')).toBe(true)
    expect(compare(false, 'Equals', 'true')).toBe(false)
  })
})
