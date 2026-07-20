import type { BehaviorEvent, RecordValue } from './behaviorTypes'

export const WEB_BEHAVIOR_EVENT_SCHEMA_VERSION = 1

export type WebBehaviorJsonValue = null | string | number | boolean | WebBehaviorJsonValue[] | { [key: string]: WebBehaviorJsonValue }

export type WebBehaviorEventMessage = {
  SchemaVersion: number
  EventType: string
  WindowId?: string | null
  WindowType?: string | null
  CanvasName?: string | null
  Timestamp: string
  Source?: string | null
  IsPreview: boolean
  Payload: Record<string, WebBehaviorJsonValue>
  Diagnostics?: string[]
}

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value)

const isJsonValue = (value: unknown, depth = 0): value is WebBehaviorJsonValue => {
  if (depth > 8 || value === null || typeof value === 'string' || typeof value === 'boolean') return depth <= 8
  if (typeof value === 'number') return Number.isFinite(value)
  if (Array.isArray(value)) return value.length <= 128 && value.every(item => isJsonValue(item, depth + 1))
  if (!isRecord(value)) return false
  const entries = Object.entries(value)
  return entries.length <= 128 && entries.every(([key, item]) => key.length > 0 && isJsonValue(item, depth + 1))
}

const optionalString = (value: unknown): value is string | null | undefined => value === undefined || value === null || typeof value === 'string'

/** Validate and decode the versioned behavior event wire message. */
export function decodeBehaviorEvent(value: unknown): BehaviorEvent | null {
  if (!isRecord(value)
    || value.SchemaVersion !== WEB_BEHAVIOR_EVENT_SCHEMA_VERSION
    || typeof value.EventType !== 'string'
    || value.EventType.length === 0
    || typeof value.Timestamp !== 'string'
    || Number.isNaN(Date.parse(value.Timestamp))
    || typeof value.IsPreview !== 'boolean'
    || !isRecord(value.Payload)
    || !isJsonValue(value.Payload)) {
    return null
  }
  if (!optionalString(value.WindowId) || !optionalString(value.WindowType) || !optionalString(value.CanvasName) || !optionalString(value.Source)) return null
  return {
    SchemaVersion: value.SchemaVersion,
    EventType: value.EventType,
    WindowId: value.WindowId,
    WindowType: value.WindowType,
    CanvasName: value.CanvasName,
    Timestamp: value.Timestamp,
    Source: value.Source,
    IsPreview: value.IsPreview,
    Payload: value.Payload as RecordValue,
  }
}
