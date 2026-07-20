import { describe, expect, it } from 'vitest'
import { WebAnimationTargetResolver } from './WebAnimationTargetResolver'

describe('shared behavior target resolver', () => {
  it('resolves MapV2 and Image-style part targets by behavior guid', () => {
    const guid = '07f7186f-30cb-9563-eb07-1164219dc777'
    document.body.innerHTML = `<div data-control-root data-control data-behavior-guid="${guid}"><div data-animation-part="PickingBorder" data-picking-border></div></div>`
    const target = new WebAnimationTargetResolver().resolve(`part:${guid}:PickingBorder`, guid, 'Auto')
    expect(target?.hasAttribute('data-picking-border')).toBe(true)
  })
})
