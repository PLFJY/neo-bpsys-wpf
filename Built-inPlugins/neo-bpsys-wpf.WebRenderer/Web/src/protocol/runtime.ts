export type WebRuntimeAsset = { Kind: 'image'; Token: string; Url: string; ContentType: string; Width?: number; Height?: number; Revision: string }
export type WebRuntimeValue = { Kind: 'null' | 'string' | 'number' | 'boolean' | 'enum' | 'color' | 'asset'; Value?: unknown; Asset?: WebRuntimeAsset }
export type RuntimeState = { values: Record<string, unknown>; sequence: number; generation: number }
export type RuntimeMessage = { type: string; payload?: { SchemaVersion?: number; Generation?: number; Sequence?: number; Values?: Record<string, WebRuntimeValue> } }

export const emptyRuntime = (): RuntimeState => ({ values: {}, sequence: 0, generation: 0 })
export const runtimeValues = (values: Record<string, WebRuntimeValue>): Record<string, unknown> =>
  Object.fromEntries(Object.entries(values).map(([path, value]) => [path, value.Kind === 'asset' ? value.Asset : value.Value]))
