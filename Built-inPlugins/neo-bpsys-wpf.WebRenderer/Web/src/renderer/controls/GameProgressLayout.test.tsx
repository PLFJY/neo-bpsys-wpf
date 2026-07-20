import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { describe, expect, it, vi } from 'vitest'
import { rotatedLayoutSize, RotatedLayoutBox } from './GameProgressLayout'

describe('RotatedLayoutBox layout measurement', () => {
  it('swaps the unrotated layout dimensions', () => {
    expect(rotatedLayoutSize(120, 28)).toEqual({ width: 28, height: 120 })
  })

  it('remeasures on ResizeObserver changes and disconnects on unmount', async () => {
    let measuredWidth = 120
    let measuredHeight = 28
    const observers: { callback: ResizeObserverCallback; disconnect: ReturnType<typeof vi.fn> }[] = []
    class FakeResizeObserver {
      callback: ResizeObserverCallback
      disconnect = vi.fn()
      constructor(callback: ResizeObserverCallback) { this.callback = callback; observers.push(this) }
      observe() { /* The component performs the initial synchronous measurement. */ }
      unobserve() { /* no-op */ }
    }
    vi.stubGlobal('ResizeObserver', FakeResizeObserver)
    Object.defineProperty(HTMLElement.prototype, 'offsetWidth', { configurable: true, get() { return this.hasAttribute('data-game-progress-measurement') ? measuredWidth : 0 } })
    Object.defineProperty(HTMLElement.prototype, 'offsetHeight', { configurable: true, get() { return this.hasAttribute('data-game-progress-measurement') ? measuredHeight : 0 } })

    const host = document.createElement('div')
    document.body.appendChild(host)
    const root = createRoot(host)
    await act(async () => { root.render(<RotatedLayoutBox direction="FacingRight"><span>FREE GAME</span></RotatedLayoutBox>) })
    expect(host.querySelector<HTMLElement>('[data-game-progress-rotated-layout]')?.style.width).toBe('28px')
    expect(host.querySelector<HTMLElement>('[data-game-progress-rotated-layout]')?.style.height).toBe('120px')

    measuredWidth = 160
    measuredHeight = 32
    await act(async () => { observers[0].callback([], observers[0] as unknown as ResizeObserver) })
    expect(host.querySelector<HTMLElement>('[data-game-progress-rotated-layout]')?.style.width).toBe('32px')
    expect(host.querySelector<HTMLElement>('[data-game-progress-rotated-layout]')?.style.height).toBe('160px')

    await act(async () => { root.unmount() })
    expect(observers[0].disconnect).toHaveBeenCalledOnce()
    host.remove()
    vi.unstubAllGlobals()
  })
})
