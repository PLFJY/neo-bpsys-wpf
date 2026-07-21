import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { CanvasRuntime } from './CanvasRuntime'
import type { Bootstrap } from '../protocol/bootstrap'
import type { RuntimeState } from '../protocol/runtime'

const runtime: RuntimeState = { values: {}, sequence: 0, generation: 0, localizationRevision: 0 }
const bootstrap: Bootstrap = {
  FullWindowType: 'GameDataWindow', DisplayName: 'GameData', Resources: { game: '/gameData.png' }, Diagnostics: [],
  Layout: { WindowSettings: { ViewboxStretch: 'Fill' }, CanvasSettings: { CanvasWidth: 1440, CanvasHeight: 810, BackgroundImage: 'game' }, ControlLayout: { Controls: {
    Negative: { ControlType: 'Rectangle', Left: 0, Top: 0, Width: 10, Height: 10, ZIndex: -1, FillColor: '#FFFFFFFF' },
    Positive: { ControlType: 'Rectangle', Left: 0, Top: 0, Width: 10, Height: 10, ZIndex: 2, FillColor: '#FFFFFFFF' },
  } } },
}

describe('CanvasRuntime layer contract', () => {
  it('renders an explicit background layer below all control layers', () => {
    const html = renderToStaticMarkup(<CanvasRuntime bootstrap={bootstrap} runtime={runtime} />)
    expect(html.indexOf('data-background-layer')).toBeGreaterThanOrEqual(0)
    expect(html.indexOf('data-background-layer')).toBeLessThan(html.indexOf('data-control-layers'))
    expect(html).toContain('background-image:url(/gameData.png)')
    expect(html).toContain('z-index:-1')
    expect(html).toContain('z-index:2')
  })
})
