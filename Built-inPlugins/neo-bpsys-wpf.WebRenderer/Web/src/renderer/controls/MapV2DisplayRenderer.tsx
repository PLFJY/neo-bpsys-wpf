import type { RuntimeState, WebRuntimeAsset } from '../../protocol/runtime'
import type { Localization } from '../../protocol/bootstrap'
import { fontFamily, fontWeight } from '../fonts'
import { finite, type MapV2DisplayConfig, type MapV2Part } from '../controlTypes'
import { PickingBorderRenderer } from '../image/PickingBorderRenderer'
import { DynamicImage } from '../image/DynamicImage'

const partNames = ['TeamName', 'MapCard', 'MapName', 'CampName', 'PickingBorder']
function part(config: MapV2DisplayConfig, name: string, fallback: { X: number; Y: number; Width: number; Height: number }) { const index = partNames.indexOf(name); return config.InternalParts?.find(item => item.Part === name || item.Part === index) ?? fallback }
function style(value: MapV2Part) { return { position: 'absolute' as const, left: finite(value.X), top: finite(value.Y), width: Math.max(1, finite(value.Width, 1)), height: Math.max(1, finite(value.Height, 1)) } }
function image(value: unknown): WebRuntimeAsset | undefined { return value && typeof value === 'object' && (value as WebRuntimeAsset).Kind === 'image' ? value as WebRuntimeAsset : undefined }
function localize(localization: Localization | undefined, key: unknown) { return typeof key === 'string' ? localization?.Values?.[key] ?? key : '' }

export function MapV2DisplayRenderer({ name, config, runtime, resources, localization }: { name: string; config: MapV2DisplayConfig; runtime: RuntimeState; resources: Record<string, string>; localization?: Localization }) {
  const mapKey = config.MapKey ?? ''; const prefix = `CurrentGame.MapV2Dictionary['${mapKey}']`; const value = (suffix: string) => runtime.values[`${prefix}.${suffix}`]
  const mapAsset = image(value('ImageSource')); const logo = image(value('OperationTeam.Logo')); const isBanned = value('IsBanned') === true; const campVisible = value('IsCampVisible') === true; const width = finite(config.Width, 200), height = finite(config.Height, 155)
  const team = part(config, 'TeamName', { X: 0, Y: 0, Width: width, Height: height / 3 }); const card = part(config, 'MapCard', { X: 5, Y: height / 3 + 5, Width: width - 10, Height: height * 4 / 9 - 10 }); const mapName = part(config, 'MapName', { X: 5, Y: height * 7 / 9 - 20, Width: width - 10, Height: 20 }); const camp = part(config, 'CampName', { X: 0, Y: height * 7 / 9, Width: width, Height: height * 2 / 9 }); const picking = part(config, 'PickingBorder', { X: 0, Y: 0, Width: width, Height: height })
  const textStyle = (family?: string, weight?: string, size?: number, color?: string) => ({ fontFamily: fontFamily(family), fontWeight: fontWeight(weight), fontSize: finite(size), color: color || '#fff' })
  return <div data-map-v2 data-control-root data-control-name={name} data-runtime-name={name} data-behavior-guid={config.BehaviorGuid} style={{ position: 'relative', width, height, overflow: 'hidden' }}>
    <div data-map-v2-part="PickingBorder" style={style(picking)}><PickingBorderRenderer name={name} behaviorGuid={config.BehaviorGuid} imagePath={config.PickingBorderImagePath} fillColor={config.PickingBorderFillColor} resources={resources} /></div>
    <div data-map-v2-part="TeamName" style={{ ...style(team), display: 'flex', alignItems: 'center', justifyContent: 'center' }}>{logo ? <DynamicImage source={logo.Url} generation={runtime.generation} style={{ width: 50, height: 50, objectFit: 'fill', borderRadius: 8 }} /> : <span style={textStyle(config.TeamNameFontFamily, config.TeamNameFontWeight, config.TeamNameFontSize || 18, config.TeamNameColor)}>{String(value('OperationTeam.Name') ?? '')}</span>}</div>
    <div data-map-v2-part="MapCard" style={{ ...style(card), overflow: 'hidden', border: `2px solid ${isBanned ? config.MapBorderBannedColor || '#9C3E2F' : config.MapBorderNormalColor || '#2B483B'}`, borderRadius: 8, boxSizing: 'border-box' }}>{mapAsset ? <DynamicImage source={mapAsset.Url} generation={runtime.generation} style={{ width: '100%', height: '100%', objectFit: 'cover' }} /> : null}</div>
    <div data-map-v2-part="MapName" style={{ ...style(mapName), display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#000', borderRadius: 4, ...textStyle(config.MapNameFontFamily, config.MapNameFontWeight, config.MapNameFontSize || 14, config.MapNameColor) }}>{localize(localization, value('MapName'))}</div>
    <div data-map-v2-part="CampName" style={{ ...style(camp), display: campVisible ? 'flex' : 'none', alignItems: 'center', justifyContent: 'space-between', ...textStyle(config.CampNameFontFamily, config.CampNameFontWeight, config.CampNameFontSize || 20, config.CampNameColor) }}><span>{localize(localization, value('OperationTeam.Camp'))}</span><img aria-label="camp-icon" src={resources[String(value('OperationTeam.Camp') ?? '').toLowerCase().includes('hun') ? 'Resources/hunIcon.png' : 'Resources/surIcon.png']} style={{ width: 30, height: 30, objectFit: 'contain' }} /></div>
  </div>
}
