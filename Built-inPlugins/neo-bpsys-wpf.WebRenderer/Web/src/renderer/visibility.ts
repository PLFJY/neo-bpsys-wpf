import type { CSSProperties } from 'react'
import { text } from './controlTypes'

export function visibilityStyle(value: unknown): CSSProperties {
  const normalized = text(value)?.toLowerCase()
  if (normalized === 'collapsed' || value === 2) return { display: 'none' }
  if (normalized === 'hidden' || value === 1) return { visibility: 'hidden' }
  return {}
}
