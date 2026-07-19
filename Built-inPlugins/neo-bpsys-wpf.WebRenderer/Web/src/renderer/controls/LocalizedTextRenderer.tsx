import { TextVisual } from '../TextVisual'
import { resolveBinding } from './TextRenderer'
import type { Localization } from '../../protocol/bootstrap'
import type { RuntimeState } from '../../protocol/runtime'
import type { LocalizedTextConfig } from '../controlTypes'

export function localize(localization: Localization | undefined, key: string, fallback?: string): string {
  if (!key) return fallback ?? ''
  return localization?.Values?.[`Any:${key}`] ?? localization?.Values?.[key] ?? fallback ?? key
}
export function LocalizedTextRenderer({ config, runtime, localization }: { config: LocalizedTextConfig; runtime: RuntimeState; localization?: Localization }) {
  const raw = resolveBinding(config.TextBinding, runtime)
  return <TextVisual config={config} runtime={runtime}>{localize(localization, raw ?? config.LocalizationKey ?? '', raw ?? config.FallbackText)}</TextVisual>
}
