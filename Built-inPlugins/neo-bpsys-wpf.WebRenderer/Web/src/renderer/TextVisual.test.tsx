import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { TextVisual } from './TextVisual'

const runtime = { values: {}, sequence: 0, generation: 0, localizationRevision: 0 }

describe('shared TextVisual layout contract', () => {
  it.each([
    ['Top', 'align-items:start'], ['Center', 'align-items:center'], ['Bottom', 'align-items:end'], ['Stretch', 'align-items:stretch'],
  ])('maps VerticalAlignment %s to the layout slot', (alignment, css) => {
    const html = renderToStaticMarkup(<TextVisual runtime={runtime} config={{ Width: 100, Height: 50, VerticalAlignment: alignment, HorizontalAlignment: 'Left', TextAlignment: 'Right' }}>value</TextVisual>)
    expect(html).toContain('data-text-layout-slot')
    expect(html).toContain(css)
    expect(html).toContain('text-align:right')
  })

  it('keeps auto-sized dimensions unconstrained', () => {
    const html = renderToStaticMarkup(<TextVisual runtime={runtime} config={{ Width: null, Height: null, HorizontalAlignment: 'Center', VerticalAlignment: 'Center' }}>multi line</TextVisual>)
    expect(html).toContain('width:max-content')
    expect(html).toContain('height:max-content')
    expect(html).toContain('data-behavior-content')
  })

  it('uses the fixed slot while allowing a non-stretch text element to retain content sizing', () => {
    const html = renderToStaticMarkup(<TextVisual runtime={runtime} config={{ Width: 200, Height: 49, HorizontalAlignment: 'Center', VerticalAlignment: 'Center' }}>-</TextVisual>)
    expect(html).toContain('width:100%')
    expect(html).toContain('height:100%')
    expect(html).toContain('data-text-element')
  })
})
