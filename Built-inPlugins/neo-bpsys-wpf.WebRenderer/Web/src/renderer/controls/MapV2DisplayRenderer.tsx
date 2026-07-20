import type { RuntimeState, WebMapV2DisplayState, WebRuntimeAsset } from '../../protocol/runtime'
import { fontFamily, fontWeight } from '../fonts'
import { finite, type MapV2DisplayConfig, type MapV2Part } from '../controlTypes'
import { PickingBorderRenderer } from '../image/PickingBorderRenderer'
import { DynamicImage } from '../image/DynamicImage'
import { AnimationPartRegistry } from '../animationParts/AnimationPartRegistry'
import type { ControlBehaviorSet } from '../../behavior/behaviorTypes'
import { SemanticControlRoot } from '../SemanticControlRoot'
import { wpfColor } from '../colors'

const partNames = ['TeamName', 'MapCard', 'MapName', 'CampName', 'PickingBorder']
function part(config: MapV2DisplayConfig, name: string, fallback: { X: number; Y: number; Width: number; Height: number }) { const index = partNames.indexOf(name); return config.InternalParts?.find(item => item.Part === name || item.Part === index) ?? fallback }
function style(value: MapV2Part) { return { position: 'absolute' as const, left: finite(value.X), top: finite(value.Y), width: Math.max(1, finite(value.Width, 1)), height: Math.max(1, finite(value.Height, 1)) } }
function image(value: unknown): WebRuntimeAsset | undefined { return value && typeof value === 'object' && (value as WebRuntimeAsset).Kind === 'image' ? value as WebRuntimeAsset : undefined }

export function MapV2DisplayRenderer({ name, controlId, config, runtime, resources, defaultPickingBorderResourceUrl, behaviorSet }: { name: string; controlId: string; config: MapV2DisplayConfig; runtime: RuntimeState; resources: Record<string, string>; defaultPickingBorderResourceUrl?: string; behaviorSet?: ControlBehaviorSet }) {
  const state = runtime.values[controlId] as WebMapV2DisplayState | undefined
  const mapAsset = image(state?.MapImage); const logo = image(state?.TeamLogo); const isBanned = state?.IsBanned === true; const campVisible = state?.IsCampVisible === true; const width = finite(config.Width, 200), height = finite(config.Height, 155)
  const team = part(config, 'TeamName', { X: 0, Y: 0, Width: width, Height: height / 3 }); const card = part(config, 'MapCard', { X: 5, Y: height / 3 + 5, Width: width - 10, Height: height * 4 / 9 - 10 }); const mapName = part(config, 'MapName', { X: 5, Y: height * 7 / 9 - 20, Width: width - 10, Height: 20 }); const camp = part(config, 'CampName', { X: 0, Y: height * 7 / 9, Width: width, Height: height * 2 / 9 }); const picking = part(config, 'PickingBorder', { X: 0, Y: 0, Width: width, Height: height })
  const textStyle = (family?: string, weight?: string, size?: number, textColor?: string) => ({ fontFamily: fontFamily(family), fontWeight: fontWeight(weight), fontSize: finite(size), color: wpfColor(textColor, '#fff') })
  const normalBorder = wpfColor(config.MapBorderNormalColor, '#2B483B')
  const bannedBorder = wpfColor(config.MapBorderBannedColor, '#9C3E2F')
  const campIcon = state?.CampKey?.toLowerCase().includes('hun') ? resources['Resources/hunIcon.png'] : resources['Resources/surIcon.png']
  return <SemanticControlRoot name={name} behaviorGuid={config.BehaviorGuid} attributes={{ 'data-map-v2': '' }} style={{ position: 'relative', width, height, overflow: 'hidden' }}>
    <div data-overlay-below><AnimationPartRegistry parts={behaviorSet?.AnimationParts} layer="BelowContent" resources={resources} /></div>
    <div data-behavior-content>
    <div data-map-v2-part="TeamName" style={{ ...style(team), display: 'flex', alignItems: 'center', justifyContent: 'center' }}>{logo ? <DynamicImage source={logo.Url} generation={runtime.generation} style={{ width: 50, height: 50, objectFit: 'fill', borderRadius: 8 }} /> : <span style={textStyle(config.TeamNameFontFamily, config.TeamNameFontWeight, config.TeamNameFontSize || 18, config.TeamNameColor)}>{state?.TeamName ?? ''}</span>}</div>
    <div data-map-v2-part="MapCard" data-map-card-border style={{ ...style(card), overflow: 'hidden', border: `2px solid ${isBanned ? bannedBorder : normalBorder}`, borderRadius: 8, boxSizing: 'border-box' }}>{mapAsset ? <DynamicImage source={mapAsset.Url} generation={runtime.generation} style={{ display: 'block', width: '100%', height: '100%', objectFit: 'cover' }} /> : null}</div>
    <div data-map-v2-part="MapName" style={{ ...style(mapName), display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#000', borderRadius: 4, ...textStyle(config.MapNameFontFamily, config.MapNameFontWeight, config.MapNameFontSize || 14, config.MapNameColor) }}>{state?.MapDisplayName ?? ''}</div>
    <div data-map-v2-part="CampName" style={{ ...style(camp), display: campVisible ? 'flex' : 'none', alignItems: 'center', justifyContent: 'space-between', ...textStyle(config.CampNameFontFamily, config.CampNameFontWeight, config.CampNameFontSize || 20, config.CampNameColor) }}><span>{state?.CampDisplayName ?? ''}</span>{campIcon ? <img aria-label="camp-icon" src={campIcon} style={{ width: 30, height: 30, objectFit: 'contain' }} /> : null}</div>
    </div>
    <div data-overlay-above><div data-map-v2-part="PickingBorder" style={style(picking)}><PickingBorderRenderer runtimeName={name} behaviorGuid={config.BehaviorGuid ?? ''} imageUrl={config.PickingBorderImagePath ? resources[config.PickingBorderImagePath] : defaultPickingBorderResourceUrl} fillColor={config.PickingBorderFillColor} zIndex={2} /></div><AnimationPartRegistry parts={behaviorSet?.AnimationParts} layer="AboveContent" resources={resources} /></div>
  </SemanticControlRoot>
}
