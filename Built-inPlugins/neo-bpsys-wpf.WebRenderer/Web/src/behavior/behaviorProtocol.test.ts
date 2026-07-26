import { describe, expect, it } from 'vitest'
import { decodeBehaviorEvent } from './behaviorProtocol'

describe('behavior event wire protocol', () => {
  it('decodes typed JSON payloads', () => {
    const event = decodeBehaviorEvent({ SchemaVersion: 1, EventType: 'Guidance.StepChanged', Timestamp: '2026-07-20T00:00:00.000Z', IsPreview: false, Payload: { Action: 'PickSur', Indexes: [0], IsPickingBorderVisible: true } })
    expect(event?.Payload.Action).toBe('PickSur')
    expect(event?.Payload.Indexes).toEqual([0])
    expect(event?.Payload.IsPickingBorderVisible).toBe(true)
  })

  it('rejects unknown schemas and unsafe payload values', () => {
    expect(decodeBehaviorEvent({ SchemaVersion: 2, EventType: 'test', Timestamp: new Date().toISOString(), IsPreview: false, Payload: {} })).toBeNull()
    expect(decodeBehaviorEvent({ SchemaVersion: 1, EventType: 'test', Timestamp: new Date().toISOString(), IsPreview: false, Payload: { value: undefined } })).toBeNull()
  })
})
