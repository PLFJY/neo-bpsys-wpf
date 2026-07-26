import { describe, expect, it, vi } from 'vitest'
import { wpfColor } from './colors'

describe('WPF color conversion', () => {
  it('converts WPF ARGB to CSS rgba in the same channel order', () => {
    expect(wpfColor('#FF000000')).toBe('rgba(0, 0, 0, 1)')
    expect(wpfColor('#FF9C3E2F')).toBe('rgba(156, 62, 47, 1)')
    expect(wpfColor('#802B483B')).toBe('rgba(43, 72, 59, 0.5019607843137255)')
  })

  it('preserves six-digit and named WPF colors', () => {
    expect(wpfColor('#2B483B')).toBe('#2B483B')
    expect(wpfColor('White')).toBe('white')
    expect(wpfColor('transparent')).toBe('transparent')
  })

  it('uses the supplied fallback for empty and invalid values', () => {
    expect(wpfColor(null, '#fff')).toBe('#fff')
    expect(wpfColor('', '#fff')).toBe('#fff')
    expect(wpfColor('not-a-color', '#fff')).toBe('#fff')
  })

  it('rate-limits invalid color diagnostics by value', () => {
    const warning = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    wpfColor('invalid-map-color-for-diagnostic-test', '#fff')
    wpfColor('invalid-map-color-for-diagnostic-test', '#fff')
    expect(warning).toHaveBeenCalledTimes(1)
    warning.mockRestore()
  })
})
