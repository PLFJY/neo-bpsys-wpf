import type { CSSProperties } from 'react'

const aliases: Record<string, string> = { '0': 'Left', '1': 'Center', '2': 'Right', '3': 'Stretch' }
const name = (value: unknown) => aliases[String(value)] ?? String(value ?? '')
export const horizontal = (value: unknown): CSSProperties['justifyContent'] => ({ Left: 'flex-start', Center: 'center', Right: 'flex-end', Stretch: 'stretch' })[name(value)] as CSSProperties['justifyContent'] ?? 'stretch'
export const vertical = (value: unknown): CSSProperties['alignItems'] => ({ Top: 'flex-start', Center: 'center', Bottom: 'flex-end', Stretch: 'stretch' })[name(value)] as CSSProperties['alignItems'] ?? 'stretch'
export const horizontalGrid = (value: unknown): CSSProperties['justifyItems'] => ({ Left: 'start', Center: 'center', Right: 'end', Stretch: 'stretch' })[name(value)] as CSSProperties['justifyItems'] ?? 'stretch'
export const verticalGrid = (value: unknown): CSSProperties['alignItems'] => ({ Top: 'start', Center: 'center', Bottom: 'end', Stretch: 'stretch' })[name(value)] as CSSProperties['alignItems'] ?? 'stretch'
export const textAlign = (value: unknown): CSSProperties['textAlign'] => ({ Left: 'left', Center: 'center', Right: 'right', Justify: 'justify' })[name(value)] as CSSProperties['textAlign'] ?? 'left'
export const wrapping = (value: unknown): CSSProperties['whiteSpace'] => {
  const valueName = name(value); return valueName === 'Wrap' ? 'pre-wrap' : valueName === 'WrapWithOverflow' ? 'pre-wrap' : 'pre'
}
