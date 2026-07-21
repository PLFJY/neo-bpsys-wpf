export type NormalizedPoint = { x: number; y: number }

type LayoutPoint = { X?: number; Y?: number }

const clamp = (value: number) => Math.max(0, Math.min(1, value))

/** Converts persisted layout points to the shared local normalized polygon contract. */
export function normalizePolygonPoints(points: LayoutPoint[] | undefined): NormalizedPoint[] | null {
  const normalized = (points ?? [])
    .filter(point => typeof point.X === 'number' && Number.isFinite(point.X)
      && typeof point.Y === 'number' && Number.isFinite(point.Y))
    .map(point => ({ x: clamp(point.X!), y: clamp(point.Y!) }))
  return normalized.length >= 3 ? normalized : null
}

/** Formats normalized points for a CSS polygon clip path without applying Canvas coordinates. */
export function polygonClipPath(points: NormalizedPoint[] | null): string | undefined {
  return points ? `polygon(${points.map(point => `${point.x * 100}% ${point.y * 100}%`).join(',')})` : undefined
}

/** Performs the shared even-odd hit test in local normalized coordinates. */
export function isPointInsidePolygon(x: number, y: number, points?: NormalizedPoint[]): boolean {
  if (!points) return true
  if (points.length < 3) return false
  let contained = false
  for (let i = 0, j = points.length - 1; i < points.length; j = i++) {
    const a = points[i], b = points[j]
    if ((a.y > y) !== (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) contained = !contained
  }
  return contained
}
