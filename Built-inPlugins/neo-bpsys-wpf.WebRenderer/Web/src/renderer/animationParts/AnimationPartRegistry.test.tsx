import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { AnimationPartRegistry } from './AnimationPartRegistry'

describe('AnimationPartRegistry', () => {
  it('renders the persisted Swipe contract rather than a hard-coded control', () => {
    const html = renderToStaticMarkup(<AnimationPartRegistry layer="AboveContent" resources={{}} parts={[{ Name: 'Swipe', Kind: 'Rectangle', Layer: 'AboveContent', Width: 4, HeightText: '100%', Fill: '#FFFFFFFF', Visibility: 'Hidden', ZIndex: 10, Effect: { Kind: 'Glow', Color: '#FFFFFFFF', Opacity: 1, BlurRadius: 30, ShadowDepth: 0, Direction: 0 } }]} />)
    expect(html).toContain('data-animation-part="Swipe"')
    expect(html).toContain('width:4px')
    expect(html).toContain('height:100%')
    expect(html).toContain('visibility:hidden')
    expect(html).toContain('drop-shadow(0px 0px 30px rgba(255, 255, 255, 1))')
  })
})
