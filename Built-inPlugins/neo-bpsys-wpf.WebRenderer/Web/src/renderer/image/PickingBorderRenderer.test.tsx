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

  it('converts the WPF fill color without changing the alpha mask layer', () => {
    const html = renderToStaticMarkup(<PickingBorderRenderer behaviorGuid="guid" runtimeName="MapCardPickingBorder" imageUrl="/pickingBorder.png" fillColor="#FF9C3E2F" zIndex={2} />)
    expect(html).toContain('background-color:rgba(156, 62, 47, 1)')
    expect(html).toContain('mask-image:url(/pickingBorder.png)')
    expect(html).toContain('opacity:0')
  })
})
