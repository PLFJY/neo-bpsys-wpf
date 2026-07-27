import { describe, expect, it, vi } from 'vitest'
import { effectColor, wpfColor } from './colors'

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

describe('effectColor (DropShadowEffect color with opacity multiplier)', () => {
  it('multiplies opacity into the ARGB alpha channel', () => {
    // opaque black at 50% opacity -> rgba(0,0,0,0.5)
    expect(effectColor('#FF000000', 0.5)).toBe('rgba(0, 0, 0, 0.5000)')
    // opaque white at 80% opacity -> rgba(255,255,255,0.8)
    expect(effectColor('#FFFFFFFF', 0.8)).toBe('rgba(255, 255, 255, 0.8000)')
  })

  it('composes with an existing alpha channel', () => {
    // #802B483B has alpha 0x80/255 ~= 0.502; at 50% opacity -> 0.2510
    expect(effectColor('#802B483B', 0.5)).toBe('rgba(43, 72, 59, 0.2510)')
  })

  it('treats six-digit RGB as fully opaque then applies opacity', () => {
    expect(effectColor('#2B483B', 1)).toBe('rgba(43, 72, 59, 1.0000)')
    expect(effectColor('#2B483B', 0.25)).toBe('rgba(43, 72, 59, 0.2500)')
  })

  it('clamps out-of-range opacity into [0,1]', () => {
    expect(effectColor('#FF000000', 2)).toBe('rgba(0, 0, 0, 1.0000)')
    expect(effectColor('#FF000000', -1)).toBe('rgba(0, 0, 0, 0.0000)')
  })

  it('falls back when the color value is empty or invalid', () => {
    expect(effectColor(null, 1, '#FF000000')).toBe('rgba(0, 0, 0, 1.0000)')
    expect(effectColor('', 1, '#FFFFFFFF')).toBe('rgba(255, 255, 255, 1.0000)')
    expect(effectColor('not-a-color', 1, '#FF880000')).toBe('rgba(136, 0, 0, 1.0000)')
  })
})
