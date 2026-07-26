import { describe, expect, it } from 'vitest'
import { formatWebLength, parseWebLength } from './WebLength'

describe('WebLength', () => {
  it.each([
    [141, '141px'], ['141', '141px'], ['141px', '141px'], ['100%', '100%'], ['0%', '0%'], ['Auto', 'auto'],
  ])('parses %p without inventing a unit', (input, expected) => {
    const value = parseWebLength(input)
    expect(value && formatWebLength(value)).toBe(expected)
  })

  it('rejects invalid compound units', () => expect(parseWebLength('100%px')).toBeUndefined())
})
