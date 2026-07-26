import { TextVisual } from '../TextVisual'
import type { WebLocalizationSnapshot } from '../../protocol/bootstrap'
import type { RuntimeState, WebLocalizedControlState } from '../../protocol/runtime'
import type { LocalizedTextConfig } from '../controlTypes'

export function LocalizedTextRenderer({ controlId, config, runtime, localization }: { controlId: string; config: LocalizedTextConfig; runtime: RuntimeState; localization?: WebLocalizationSnapshot }) {
  const runtimeValue = runtime.values[controlId] as WebLocalizedControlState | undefined
  const staticValue = localization?.StaticTexts[controlId]
  const displayText = runtimeValue?.DisplayText ?? staticValue ?? ''
  return <TextVisual config={config} runtime={runtime}>{displayText}</TextVisual>
}
