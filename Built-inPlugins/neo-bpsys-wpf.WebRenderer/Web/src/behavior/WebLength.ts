export type WebLength =
  | { kind: 'px'; value: number }
  | { kind: 'percent'; value: number }
  | { kind: 'auto' }

export function parseWebLength(value: unknown): WebLength | undefined {
  if (typeof value === 'number' && Number.isFinite(value)) return { kind: 'px', value }
  if (typeof value !== 'string') return undefined
  const text = value.trim()
  if (text.toLowerCase() === 'auto') return { kind: 'auto' }
  const percent = /^([+-]?(?:\d+(?:\.\d+)?|\.\d+))%$/.exec(text)
  if (percent) return { kind: 'percent', value: Number(percent[1]) }
  const pixels = /^([+-]?(?:\d+(?:\.\d+)?|\.\d+))(?:px)?$/i.exec(text)
  return pixels ? { kind: 'px', value: Number(pixels[1]) } : undefined
}

export function formatWebLength(value: WebLength): string {
  if (value.kind === 'auto') return 'auto'
  return `${value.value}${value.kind === 'percent' ? '%' : 'px'}`
}

export function resolveWebLength(value: WebLength, reference: number): number | undefined {
  if (value.kind === 'auto') return undefined
  return value.kind === 'percent' ? reference * value.value / 100 : value.value
}
