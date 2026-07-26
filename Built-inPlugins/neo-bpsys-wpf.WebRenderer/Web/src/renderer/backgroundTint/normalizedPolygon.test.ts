import { describe, expect, it, vi } from 'vitest'
import gameDataLayout from '../../../../../../neo-bpsys-wpf/Resources/FrontedLayouts/GameDataWindow.json'
import { isPointInsidePolygon, normalizePolygonPoints, polygonClipPath } from './normalizedPolygon'

describe('normalized BackgroundTint polygons', () => {
  it('keeps points local and never subtracts Canvas Left/Top', () => {
    const points = normalizePolygonPoints([{ X: 0, Y: 0 }, { X: 1, Y: 0.5 }, { X: 0.25, Y: 1 }])
    expect(points).toEqual([{ x: 0, y: 0 }, { x: 1, y: 0.5 }, { x: 0.25, y: 1 }])
    expect(polygonClipPath(points)).toBe('polygon(0% 0%,100% 50%,25% 100%)')
  })

  it('matches WPF clamping for finite out-of-range points', () => {
    expect(normalizePolygonPoints([{ X: -1, Y: 2 }, { X: 0.5, Y: 0 }, { X: 1.5, Y: 1 }, { X: 0, Y: 1 }])).toEqual([
      { x: 0, y: 1 }, { x: 0.5, y: 0 }, { x: 1, y: 1 }, { x: 0, y: 1 },
    ])
  })

  it('rejects fewer than three finite points without a fallback polygon', () => {
    expect(normalizePolygonPoints([{ X: 0, Y: 0 }, { X: Number.NaN, Y: 1 }, { X: 1, Y: Number.POSITIVE_INFINITY }])).toBeNull()
    expect(polygonClipPath(null)).toBeUndefined()
  })

  it('uses the same even-odd hit test for winding direction and concave shapes', () => {
    const concave = normalizePolygonPoints([{ X: 0, Y: 0 }, { X: 1, Y: 0 }, { X: 1, Y: 1 }, { X: 0.5, Y: 0.5 }, { X: 0, Y: 1 }])!
    expect(isPointInsidePolygon(0.2, 0.2, concave)).toBe(true)
    expect(isPointInsidePolygon(0.75, 0.75, concave)).toBe(true)
    expect(isPointInsidePolygon(0.5, 0.75, concave)).toBe(false)
    expect(isPointInsidePolygon(0.2, 0.2, [...concave].reverse())).toBe(true)
  })

  it('keeps both built-in GameData polygons in their local control coordinate system', () => {
    const controls = (gameDataLayout as { ControlLayout: { Controls: Record<string, { ControlType: string; Left: number; Top: number; Points?: { X?: number; Y?: number }[] }> } }).ControlLayout.Controls
    for (const name of ['BackgroundTintSur', 'BackgroundTintHun']) {
      const control = controls[name]
      const points = normalizePolygonPoints(control.Points)
      expect(control.ControlType).toBe('BackgroundTintPolygon')
      expect(points).not.toBeNull()
      expect(points!.every(point => point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1)).toBe(true)
      expect(polygonClipPath(points)).not.toContain(`${control.Left}%`)
      expect(polygonClipPath(points)).not.toContain(`${control.Top}%`)
    }
  })

  it('does not emit repeated diagnostics for the same invalid polygon key', () => {
    const warning = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    // Diagnostic ownership is exercised by BackgroundTintRenderer; this test documents that
    // the pure contract itself does not silently invent a default shape.
    expect(normalizePolygonPoints([])).toBeNull()
    expect(normalizePolygonPoints([])).toBeNull()
    expect(warning).not.toHaveBeenCalled()
    warning.mockRestore()
  })
})
