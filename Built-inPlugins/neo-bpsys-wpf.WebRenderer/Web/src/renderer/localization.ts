import type { Localization } from '../protocol/bootstrap'
const reported = new Set<string>()
export function localize(snapshot: Localization | undefined, dictionary: string, key: string, fallback = ''): string {
  const value = snapshot?.Dictionaries?.[dictionary]?.[key]
  if (value !== undefined) return value
  const anyHost = snapshot?.AnyHost?.[key]
  if (anyHost !== undefined) return anyHost
  const id = `${dictionary}:${key}`
  if (key && !reported.has(id)) { reported.add(id); console.warn(`[Web Renderer] LocalizationMissing:${id}`) }
  return fallback
}
