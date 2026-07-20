import type { ReactNode } from 'react'
import { ControlFrame } from './ControlFrame'
import { TextRenderer } from './controls/TextRenderer'
import { LocalizedTextRenderer } from './controls/LocalizedTextRenderer'
import { MapNameTextRenderer } from './controls/MapNameTextRenderer'
import { GameProgressTextRenderer } from './controls/GameProgressTextRenderer'
import { PolygonRenderer, RectangleRenderer } from './controls/ShapeRenderers'
import { BorderedImageRenderer, ImageRenderer } from './controls/ImageRenderers'
import type { Localization } from '../protocol/bootstrap'
import type { RuntimeState } from '../protocol/runtime'
import type { ControlConfig } from './controlTypes'

const diagnosed = new Set<string>()
function Unsupported({ name, type }: { name: string; type: string }) { const key = `${type}:${name}`; if (!diagnosed.has(key)) { diagnosed.add(key); console.warn(`[Web Renderer] ${type} is not implemented for ${name}.`) } return <div data-unsupported-control={type} /> }
export function WebControlRegistry({ name, config, runtime, localization, resources }: { name: string; config: ControlConfig; runtime: RuntimeState; localization?: Localization; resources: Record<string, string> }) {
  let control: ReactNode
  switch (config.ControlType) {
    case 'Text': control = <TextRenderer config={config} runtime={runtime} />; break
    case 'LocalizedText': control = <LocalizedTextRenderer config={config} runtime={runtime} localization={localization} />; break
    case 'MapNameText': control = <MapNameTextRenderer config={config} runtime={runtime} localization={localization} />; break
    case 'GameProgressText': control = <GameProgressTextRenderer config={config} runtime={runtime} localization={localization} />; break
    case 'Rectangle': control = <RectangleRenderer config={config} runtime={runtime} />; break
    case 'Polygon': control = <PolygonRenderer config={config} runtime={runtime} />; break
    case 'Image': control = <ImageRenderer name={name} config={config} runtime={runtime} resources={resources} />; break
    case 'BorderedImage': control = <BorderedImageRenderer name={name} config={config} runtime={runtime} resources={resources} />; break
    default: control = <Unsupported name={name} type={config.ControlType} />
  }
  return <ControlFrame name={name} config={config}>{control}</ControlFrame>
}
