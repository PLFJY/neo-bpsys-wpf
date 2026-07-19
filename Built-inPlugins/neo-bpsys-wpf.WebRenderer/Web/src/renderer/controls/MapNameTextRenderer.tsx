import { TextVisual } from '../TextVisual'
import type { Localization } from '../../protocol/bootstrap'
import type { RuntimeState } from '../../protocol/runtime'
import type { MapNameConfig } from '../controlTypes'

export function MapNameTextRenderer({ config, runtime, localization }: { config: MapNameConfig; runtime: RuntimeState; localization?: Localization }) {
  const path = config.BindingPath || 'CurrentGame.PickedMap'; const raw = runtime.values[path]
  const map = raw == null ? undefined : String(raw)
  const value = map ? localization?.Values?.[`Game:${map}`] ?? map : config.EmptyText ?? ''
  return <TextVisual config={config} runtime={runtime}>{value}</TextVisual>
}
