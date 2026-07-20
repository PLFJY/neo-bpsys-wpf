import { TextVisual } from '../TextVisual'
import { localize } from '../localization'
import { resolveBinding } from './TextRenderer'
import type { Localization } from '../../protocol/bootstrap'
import type { RuntimeState } from '../../protocol/runtime'
import type { LocalizedTextConfig } from '../controlTypes'

export function LocalizedTextRenderer({ config, runtime, localization }: { config: LocalizedTextConfig; runtime: RuntimeState; localization?: Localization }) {
  const raw = resolveBinding(config.TextBinding, runtime)
  return <TextVisual config={config} runtime={runtime}>{localize(localization, 'Fronted', raw ?? config.LocalizationKey ?? '', config.FallbackText ?? '')}</TextVisual>
}
