import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { PickingBorderRenderer } from './PickingBorderRenderer'

describe('PickingBorderRenderer', () => {
  it('keeps the configured runtime name and stays transparent when a mask is missing', () => {
    const html = renderToStaticMarkup(<PickingBorderRenderer behaviorGuid="guid" runtimeName="SurPickingBorder0" zIndex={2} />)
    expect(html).toContain('data-animation-part="PickingBorder"')
    expect(html).toContain('data-runtime-name="SurPickingBorder0"')
    expect(html).toContain('background-color:transparent')
  })
})
