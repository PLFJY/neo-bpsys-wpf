import { renderToStaticMarkup } from 'react-dom/server'
import type { CSSProperties } from 'react'
import { describe, expect, it, vi } from 'vitest'
import type { RuntimeState, WebMapV2DisplayState, WebRuntimeAsset } from '../../protocol/runtime'
import type { MapV2DisplayConfig } from '../controlTypes'

vi.mock('../image/DynamicImage', () => ({
  DynamicImage: ({ style }: { style: CSSProperties }) => <img data-image-element style={style} />,
}))

import { MapV2DisplayRenderer } from './MapV2DisplayRenderer'

const config = (overrides: Partial<MapV2DisplayConfig> = {}): MapV2DisplayConfig => ({
  ControlType: 'MapV2Display',
  Width: 200,
  Height: 155,
  MapBorderNormalColor: '#FF000000',
  MapBorderBannedColor: '#FF9C3E2F',
  MapNameColor: '#FF000000',
  TeamNameColor: '#FF000000',
  CampNameColor: '#FF000000',
  PickingBorderFillColor: '#802B483B',
  ...overrides,
})

const state = (overrides: Partial<WebMapV2DisplayState> = {}): WebMapV2DisplayState => ({
  MapKey: 'ArmsFactory',
  MapDisplayName: 'Arms Factory',
  CampDisplayName: 'Survivor',
  TeamName: 'Team',
  IsBanned: false,
  IsPicked: false,
  IsCampVisible: true,
  CampKey: 'Sur',
  MapImage: { Kind: 'image', SourceKind: 'frozen', Token: 'map', Url: '/map.png', ContentType: 'image/png', Revision: '1' } as WebRuntimeAsset,
  ...overrides,
})

const markup = (mapState: WebMapV2DisplayState, mapConfig: MapV2DisplayConfig = config()) => {
  const runtime: RuntimeState = { values: { map: mapState }, sequence: 1, generation: 1, localizationRevision: 1 }
  return renderToStaticMarkup(<MapV2DisplayRenderer name="MapCard" controlId="map" config={mapConfig} runtime={runtime} resources={{}} defaultPickingBorderResourceUrl="/pickingBorder.png" />)
}

describe('MapV2DisplayRenderer colors and state semantics', () => {
  it('uses the WPF-converted normal border and keeps image inside the bordered card', () => {
    const html = markup(state())
    expect(html).toContain('data-map-card-border')
    expect(html).toContain('border:2px solid rgba(0, 0, 0, 1)')
    expect(html).toContain('overflow:hidden;border:2px solid rgba(0, 0, 0, 1);border-radius:8px;box-sizing:border-box')
    expect(html).toContain('display:block;width:100%;height:100%;object-fit:cover')
    expect(html).not.toContain('border:2px solid #FF000000')
  })

  it('switches only the card border when IsBanned changes', () => {
    const normal = markup(state({ IsBanned: false, IsPicked: true }))
    const banned = markup(state({ IsBanned: true, IsPicked: true }))
    expect(normal).toContain('border:2px solid rgba(0, 0, 0, 1)')
    expect(banned).toContain('border:2px solid rgba(156, 62, 47, 1)')
    expect(banned).not.toContain('border:2px solid rgba(0, 0, 0, 1)')
  })

  it('does not invent an IsPicked border and converts text colors too', () => {
    const picked = markup(state({ IsPicked: true }))
    expect(picked.match(/border:2px solid/g)).toHaveLength(1)
    expect(picked).toContain('color:rgba(0, 0, 0, 1)')
  })

  it('keeps a separate picking overlay with the converted fill color', () => {
    const html = markup(state(), config({ PickingBorderFillColor: '#802B483B' }))
    expect(html).toContain('data-picking-border')
    expect(html).toContain('background-color:rgba(43, 72, 59, 0.5019607843137255)')
    expect(html).toContain('mask-image:url(/pickingBorder.png)')
  })
})
