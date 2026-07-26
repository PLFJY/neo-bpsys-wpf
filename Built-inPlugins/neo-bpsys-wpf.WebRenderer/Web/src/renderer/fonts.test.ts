import { describe, expect, it } from 'vitest'
import { fontFamily, fontWeight, isEmbeddedFontReference } from './fonts'

describe('web fonts', () => {
  it('keeps ordinary system font families for browser resolution', () => {
    for (const family of ['Arial', 'Segoe UI', 'Microsoft YaHei', 'Times New Roman', 'sans-serif', 'serif', 'monospace']) {
      expect(fontFamily(family)).toBe(family)
      expect(isEmbeddedFontReference(family)).toBe(false)
    }
  })

  it('recognizes explicit pack and package font references', () => {
    expect(isEmbeddedFontReference('pack://application:,,,/Assets/Fonts/#Noto Sans')).toBe(true)
    expect(isEmbeddedFontReference('bpui://package/Resources/fonts/custom.ttf#Custom')).toBe(true)
    expect(isEmbeddedFontReference('Resources/fonts/custom.woff2#Custom')).toBe(true)
    expect(fontFamily('Resources/fonts/custom.woff2#Custom')).toBe('Custom')
  })

  it('maps WPF font weight names to CSS numeric values', () => {
    expect(fontWeight('Medium')).toBe(500)
    expect([fontWeight('Thin'), fontWeight('ExtraLight'), fontWeight('UltraLight'), fontWeight('Light'),
      fontWeight('Normal'), fontWeight('Regular'), fontWeight('DemiBold'), fontWeight('SemiBold'),
      fontWeight('Bold'), fontWeight('ExtraBold'), fontWeight('UltraBold'), fontWeight('Black'),
      fontWeight('Heavy'), fontWeight('ExtraBlack')])
      .toEqual([100, 200, 200, 300, 400, 400, 600, 600, 700, 800, 800, 900, 900, 950])
    expect(fontWeight('unknown')).toBeUndefined()
  })
})
