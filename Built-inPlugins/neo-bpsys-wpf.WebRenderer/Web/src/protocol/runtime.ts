export type WebRuntimeAsset = {
  Kind: 'image'
  SourceKind: 'local' | 'remote' | 'frozen'
  Token: string
  Url: string
  ContentType: string
  NaturalWidthDip?: number
  NaturalHeightDip?: number
  PixelWidth?: number
  PixelHeight?: number
  DpiX?: number
  DpiY?: number
  /** Legacy protocol aliases, read only. */
  Width?: number
  Height?: number
  Revision: string
}
export type WebRuntimeValueState = 'resolved' | 'pending' | 'null' | 'failed'
export type WebLocalizedControlState = { ControlId: string; DisplayText: string }
export type WebMapV2DisplayState = { MapKey: string; MapDisplayName: string; CampDisplayName: string; TeamName: string; TeamLogo?: WebRuntimeAsset; MapImage?: WebRuntimeAsset; IsBanned: boolean; IsPicked: boolean; IsCampVisible: boolean; CampKey?: string }
export type WebGameProgressDisplayState = { IsValid: boolean; IsFree: boolean; GameNumber?: number; IsOvertime: boolean; Half?: string; FullText: string; GameText: string; HalfText: string; IsCjkCulture: boolean }
export type WebRuntimeValue = { State?: WebRuntimeValueState; Kind: 'null' | 'string' | 'number' | 'boolean' | 'enum' | 'color' | 'asset' | 'localizedControl' | 'mapName' | 'mapV2Display' | 'gameProgressDisplay'; Value?: unknown; Asset?: WebRuntimeAsset; Diagnostic?: string }
export type RuntimeState = { values: Record<string, unknown>; sequence: number; generation: number; localizationRevision: number }
export type RuntimeMessage = { type: string; payload?: { SchemaVersion?: number; Generation?: number; Sequence?: number; LocalizationRevision?: number; Values?: Record<string, WebRuntimeValue> } }

export const emptyRuntime = (): RuntimeState => ({ values: {}, sequence: 0, generation: 0, localizationRevision: 0 })
export const resolvedRuntimeValue = (value: WebRuntimeValue): unknown => value.Kind === 'asset' ? value.Asset : value.Value
