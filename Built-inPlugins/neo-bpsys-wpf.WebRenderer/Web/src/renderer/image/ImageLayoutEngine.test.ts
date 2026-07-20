import { describe, expect, it } from 'vitest'
import { calculateImageLayout } from './ImageLayoutEngine'

const base = { controlType: 'BorderedImage' as const, outerWidth: 141, outerHeight: 161, naturalWidth: 1000, naturalHeight: 2000, sizingMode: 'OverflowCrop', stretch: 'UniformToFill', horizontalAlignment: 'Center', verticalAlignment: 'Center' }

describe('ImageLayoutEngine', () => {
  it('scales a portrait to cover a landscape-ish survivor slot before cropping', () => {
    const value = calculateImageLayout(base)
    expect(value.imageLayoutWidth).toBeCloseTo(141)
    expect(value.imageLayoutHeight).toBeCloseTo(282)
    expect(value.imageOffsetY).toBeCloseTo(-60.5)
  })

  it('scales an ultra-wide source to cover a portrait slot', () => {
    const value = calculateImageLayout({ ...base, outerWidth: 100, outerHeight: 200, naturalWidth: 2000, naturalHeight: 500 })
    expect(value.imageLayoutWidth).toBeCloseTo(800)
    expect(value.imageLayoutHeight).toBeCloseTo(200)
    expect(value.imageOffsetX).toBeCloseTo(-350)
  })

  it('keeps a square logo uniform inside its slot', () => {
    const value = calculateImageLayout({ ...base, outerWidth: 200, outerHeight: 100, naturalWidth: 500, naturalHeight: 500, sizingMode: 'FillContainer', stretch: 'Uniform' })
    expect(value.imageLayoutWidth).toBeCloseTo(100)
    expect(value.imageLayoutHeight).toBeCloseTo(100)
    expect(value.imageOffsetX).toBeCloseTo(50)
  })

  it('applies explicit inner image dimensions without changing the outer viewport', () => {
    const width = calculateImageLayout({ ...base, outerWidth: 100, outerHeight: 100, imageWidth: 240, stretch: 'Fill' })
    const height = calculateImageLayout({ ...base, outerWidth: 100, outerHeight: 100, imageHeight: 180, stretch: 'Fill' })
    expect(width.imageLayoutWidth).toBe(240)
    expect(width.viewportStyle.width).toBe('100%')
    expect(height.imageLayoutHeight).toBe(180)
  })

  it('supports null inner dimensions, Fill, Uniform, UniformToFill and None', () => {
    expect(calculateImageLayout({ ...base, imageWidth: null, imageHeight: null, stretch: 'Fill' }).imageLayoutWidth).toBe(141)
    expect(calculateImageLayout({ ...base, stretch: 'Uniform' }).imageLayoutHeight).toBeCloseTo(161)
    expect(calculateImageLayout(base).imageLayoutHeight).toBeCloseTo(282)
    expect(calculateImageLayout({ ...base, stretch: 'None' }).imageLayoutWidth).toBe(1000)
  })

  it('supports Auto, FillContainer and OverflowCrop alignment defaults', () => {
    expect(calculateImageLayout({ ...base, sizingMode: 'Auto', stretch: 'Uniform' }).imageOffsetX).toBeGreaterThan(0)
    expect(calculateImageLayout({ ...base, sizingMode: 'FillContainer', stretch: 'Fill' }).imageOffsetX).toBe(0)
    expect(calculateImageLayout(base).viewportStyle.overflow).toBe('hidden')
  })

  it('supports left/right/top/bottom alignment', () => {
    const left = calculateImageLayout({ ...base, stretch: 'None', horizontalAlignment: 'Left', verticalAlignment: 'Top' })
    const right = calculateImageLayout({ ...base, stretch: 'None', horizontalAlignment: 'Right', verticalAlignment: 'Bottom' })
    expect(left.imageOffsetX).toBe(0); expect(left.imageOffsetY).toBe(0)
    expect(right.imageOffsetX).toBe(141 - 1000); expect(right.imageOffsetY).toBe(161 - 2000)
  })
})
