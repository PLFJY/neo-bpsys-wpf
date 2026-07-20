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
export type WebRuntimeValue = { State?: WebRuntimeValueState; Kind: 'null' | 'string' | 'number' | 'boolean' | 'enum' | 'color' | 'asset'; Value?: unknown; Asset?: WebRuntimeAsset; Diagnostic?: string }
export type RuntimeState = { values: Record<string, unknown>; sequence: number; generation: number }
export type RuntimeMessage = { type: string; payload?: { SchemaVersion?: number; Generation?: number; Sequence?: number; Values?: Record<string, WebRuntimeValue> } }

export const emptyRuntime = (): RuntimeState => ({ values: {}, sequence: 0, generation: 0 })
export const resolvedRuntimeValue = (value: WebRuntimeValue): unknown => value.Kind === 'asset' ? value.Asset : value.Value
