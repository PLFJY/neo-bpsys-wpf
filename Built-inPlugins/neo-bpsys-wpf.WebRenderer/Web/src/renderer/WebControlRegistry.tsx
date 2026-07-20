import type { ReactNode } from 'react'
import { ControlFrame } from './ControlFrame'
import { TextRenderer } from './controls/TextRenderer'
import { LocalizedTextRenderer } from './controls/LocalizedTextRenderer'
import { MapNameTextRenderer } from './controls/MapNameTextRenderer'
import { GameProgressTextRenderer } from './controls/GameProgressTextRenderer'
import { PolygonRenderer, RectangleRenderer } from './controls/ShapeRenderers'
import { BorderedImageRenderer } from './image/BorderedImageRenderer'
import { ImageRenderer } from './image/ImageRenderer'
import type { Localization } from '../protocol/bootstrap'
import type { RuntimeState } from '../protocol/runtime'
import type { ControlConfig } from './controlTypes'
import type { ControlBehaviorSet } from '../behavior/behaviorTypes'
import type { WebRenderContext } from './WebRenderContext'
import { BackgroundTintRenderer } from './backgroundTint/BackgroundTintRenderer'
import { MapV2DisplayRenderer } from './controls/MapV2DisplayRenderer'

const diagnosed = new Set<string>()
function Unsupported({ name, type }: { name: string; type: string }) { const key = `${type}:${name}`; if (!diagnosed.has(key)) { diagnosed.add(key); console.warn(`[Web Renderer] ${type} is not implemented for ${name}.`) } return <div data-unsupported-control={type} /> }
export function WebControlRegistry({ name, config, runtime, localization, resources, context, behaviorSet }: { name: string; config: ControlConfig; runtime: RuntimeState; localization?: Localization; resources: Record<string, string>; context: WebRenderContext; behaviorSet?: ControlBehaviorSet }) {
  let control: ReactNode
  let semanticChild = false
  switch (config.ControlType) {
    case 'Text': control = <TextRenderer config={config} runtime={runtime} />; break
    case 'LocalizedText': control = <LocalizedTextRenderer config={config} runtime={runtime} localization={localization} />; break
    case 'MapNameText': control = <MapNameTextRenderer config={config} runtime={runtime} localization={localization} />; break
    case 'GameProgressText': control = <GameProgressTextRenderer config={config} runtime={runtime} localization={localization} />; break
    case 'Rectangle': control = <RectangleRenderer config={config} runtime={runtime} />; break
    case 'Polygon': control = <PolygonRenderer config={config} runtime={runtime} />; break
    case 'BackgroundTintRectangle': case 'BackgroundTintPolygon': control = <BackgroundTintRenderer config={config} runtime={runtime} context={context} />; break
    case 'MapV2Display': semanticChild = true; control = <MapV2DisplayRenderer name={name} config={config} runtime={runtime} resources={resources} localization={localization} />; break
    case 'Image': semanticChild = true; control = <ImageRenderer name={name} config={config} runtime={runtime} resources={resources} behaviorSet={behaviorSet} />; break
    case 'BorderedImage': semanticChild = true; control = <BorderedImageRenderer name={name} config={config} runtime={runtime} resources={resources} behaviorSet={behaviorSet} />; break
    default: control = <Unsupported name={name} type={config.ControlType} />
  }
  return <ControlFrame name={name} config={config} semanticChild={semanticChild} behaviorSet={behaviorSet} resources={resources}>{control}</ControlFrame>
}
