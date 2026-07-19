export const fontFamily = (value: unknown): string | undefined => {
  if (typeof value !== 'string' || !value) return undefined
  const index = value.lastIndexOf('#'); return index >= 0 ? value.slice(index + 1) : value
}
