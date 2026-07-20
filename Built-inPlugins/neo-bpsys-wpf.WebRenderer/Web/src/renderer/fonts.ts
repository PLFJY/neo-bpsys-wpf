const weights: Record<string, number> = {
  thin: 100,
  extralight: 200,
  ultralight: 200,
  light: 300,
  normal: 400,
  regular: 400,
  medium: 500,
  demibold: 600,
  semibold: 600,
  bold: 700,
  extrabold: 800,
  ultrabold: 800,
  black: 900,
  heavy: 900,
  extrablack: 950,
}

export const fontFamily = (value: unknown): string | undefined => {
  if (typeof value !== 'string' || !value.trim()) return undefined
  const index = value.lastIndexOf('#')
  return index >= 0 ? value.slice(index + 1) : value
}

export const fontWeight = (value: unknown): number | undefined => {
  if (typeof value !== 'string') return undefined
  return weights[value.trim().replace(/[\s-]/g, '').toLowerCase()]
}

export const isEmbeddedFontReference = (value: unknown): boolean => {
  if (typeof value !== 'string') return false
  if (/^pack:\/\//i.test(value)) return value.includes('#')
  if (!/^(bpui:\/\/|Resources\/)/i.test(value)) return false
  return /\.(ttf|otf|woff2?)(?:#|$)/i.test(value)
}
