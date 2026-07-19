export function color(value: unknown, fallback = 'transparent'): string {
  if (typeof value !== 'string') return fallback
  const match = /^#([0-9a-f]{8})$/i.exec(value)
  if (match) { const h = match[1]; return `rgba(${parseInt(h.slice(2, 4), 16)}, ${parseInt(h.slice(4, 6), 16)}, ${parseInt(h.slice(6, 8), 16)}, ${parseInt(h.slice(0, 2), 16) / 255})` }
  return /^#[0-9a-f]{6}$/i.test(value) ? value : fallback
}
