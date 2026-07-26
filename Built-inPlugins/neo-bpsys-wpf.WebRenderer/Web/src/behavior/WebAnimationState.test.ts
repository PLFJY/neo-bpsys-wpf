import { describe, expect, it, vi } from 'vitest'
import { WebAnimatablePropertyAdapterRegistry } from './WebAnimatablePropertyAdapters'

function animatedElement() {
  const element = document.createElement('div')
  let frames: Keyframe[] = []
  const animation = { finished: Promise.resolve(), cancel: vi.fn() } as unknown as Animation
  element.animate = vi.fn((value) => { frames = value as Keyframe[]; return animation })
  return { element, frames: () => frames }
}

describe('Web animation state', () => {
  it('keeps percentages valid in left/right clip-path keyframes', async () => {
    const registry = new WebAnimatablePropertyAdapterRegistry(); const left = animatedElement()
    await registry.animate(left.element, 'ClipInsetLeft', '100%', '0%', 100, true, new AbortController().signal)
    expect(left.frames()).toEqual([{ clipPath: 'inset(0px 0px 0px 100%)' }, { clipPath: 'inset(0px 0px 0px 0%)' }])
    const right = animatedElement()
    await registry.animate(right.element, 'ClipInsetRight', '0%', '100%', 100, true, new AbortController().signal)
    expect(right.frames()).toEqual([{ clipPath: 'inset(0px 0% 0px 0px)' }, { clipPath: 'inset(0px 100% 0px 0px)' }])
  })

  it('uses independent transform components for parallel offset, scale and rotation', async () => {
    const registry = new WebAnimatablePropertyAdapterRegistry(); const target = animatedElement()
    registry.set(target.element, 'ScaleX', 2); registry.set(target.element, 'ScaleY', 3); registry.set(target.element, 'Rotation', 15)
    await registry.animate(target.element, 'VisualOffsetX', 0, 20, 100, true, new AbortController().signal)
    expect(target.frames()[1].translate).toBe('20px 0px')
    expect(target.element.style.scale).toBe('2 3')
    expect(target.element.style.rotate).toBe('15deg')
  })

  it('Reset All restores the first baseline without recapturing animated values', () => {
    const registry = new WebAnimatablePropertyAdapterRegistry(); const element = document.createElement('div')
    element.style.opacity = '0.6'; registry.set(element, 'Opacity', 0.2); registry.set(element, 'ScaleX', 2)
    registry.reset(element, 'All')
    expect(element.style.opacity).toBe('0.6')
    expect(registry.getState(element).scaleX).toBe(1)
  })

  it('supports cancellation and non-waiting animation ownership', async () => {
    const registry = new WebAnimatablePropertyAdapterRegistry(); const target = animatedElement(); const controller = new AbortController()
    await expect(registry.animate(target.element, 'Opacity', 0, 1, 100, false, controller.signal)).resolves.toBe(true)
    controller.abort()
    expect((target.element.animate as ReturnType<typeof vi.fn>)).toHaveBeenCalledOnce()
  })
})
