import { TextVisual } from '../TextVisual'
import type { RuntimeState, WebLocalizedControlState } from '../../protocol/runtime'
import type { MapNameConfig } from '../controlTypes'

export function MapNameTextRenderer({ controlId, config, runtime }: { controlId: string; config: MapNameConfig; runtime: RuntimeState }) {
  const state = runtime.values[controlId] as WebLocalizedControlState | undefined
  return <TextVisual config={config} runtime={runtime}>{state?.DisplayText ?? ''}</TextVisual>
}
