/// <reference lib="webworker" />
type Point = { X?: number; Y?: number }
type TintRequest = { type: 'render'; id: number; revision: string; width: number; height: number; canvasWidth: number; canvasHeight: number; left: number; top: number; tint: string; mode: number; tintStrength: number; textureStrength: number; points?: Point[] }
let source: ImageBitmap | undefined
let sourceRevision = ''
const cache = new Map<string, ImageData>()
const color = (value: string) => { const hex = /^#?([\da-f]{6}|[\da-f]{8})$/i.exec(value)?.[1] ?? 'ffffff'; const o = hex.length === 8 ? 2 : 0; return [parseInt(hex.slice(o, o + 2), 16), parseInt(hex.slice(o + 2, o + 4), 16), parseInt(hex.slice(o + 4, o + 6), 16)] }
const luminance = (r: number, g: number, b: number) => r * .2126 + g * .7152 + b * .0722
const clamp = (v: number) => Math.max(0, Math.min(255, Math.round(v)))
function inside(x: number, y: number, points?: Point[]) { if (!points) return true; if (points.length < 3) return false; let contained = false; for (let i = 0, j = points.length - 1; i < points.length; j = i++) { const a = points[i], b = points[j], ax = a.X ?? Number.NaN, ay = a.Y ?? Number.NaN, bx = b.X ?? Number.NaN, by = b.Y ?? Number.NaN; if (![ax, ay, bx, by].every(Number.isFinite)) return false; if ((ay > y) !== (by > y) && x < (bx - ax) * (y - ay) / (by - ay) + ax) contained = !contained } return contained }
function render(request: TintRequest) {
  if (!source || sourceRevision !== request.revision) throw new Error('BackgroundUnavailable')
  const key = JSON.stringify(request), cached = cache.get(key); if (cached) return cached
  const bitmap = source
  const left = Math.max(0, Math.floor(request.left)), top = Math.max(0, Math.floor(request.top)), width = Math.max(1, Math.floor(request.width)), height = Math.max(1, Math.floor(request.height))
  const input = new OffscreenCanvas(bitmap.width, bitmap.height), context = input.getContext('2d', { willReadFrequently: true })!; context.drawImage(bitmap, 0, 0)
  const sourceData = context.getImageData(0, 0, bitmap.width, bitmap.height), output = new ImageData(width, height), tint = color(request.tint), strength = Math.max(0, Math.min(1, request.tintStrength)), texture = Math.max(0, Math.min(1, request.textureStrength))
  const sample = (x: number, y: number) => { const sx = Math.min(bitmap.width - 1, Math.max(0, Math.floor(x * bitmap.width / request.canvasWidth))), sy = Math.min(bitmap.height - 1, Math.max(0, Math.floor(y * bitmap.height / request.canvasHeight))); return (sy * bitmap.width + sx) * 4 }
  let total = 0, count = 0
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) { if (!inside((x + .5) / width, (y + .5) / height, request.points)) continue; const p = sample(left + x + .5, top + y + .5); if (sourceData.data[p + 3]) { total += luminance(sourceData.data[p], sourceData.data[p + 1], sourceData.data[p + 2]); count++ } }
  const average = count ? total / count : 0
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) { const out = (y * width + x) * 4; if (!inside((x + .5) / width, (y + .5) / height, request.points)) continue; const p = sample(left + x + .5, top + y + .5), r = sourceData.data[p], g = sourceData.data[p + 1], b = sourceData.data[p + 2], l = luminance(r, g, b); let tr: number, tg: number, tb: number; if (request.mode === 0) { tr = r * tint[0] / 255; tg = g * tint[1] / 255; tb = b * tint[2] / 255 } else if (request.mode === 2) { const detail = (l - average) * texture; tr = tint[0] + detail; tg = tint[1] + detail; tb = tint[2] + detail } else { tr = l * tint[0] / 255; tg = l * tint[1] / 255; tb = l * tint[2] / 255 } output.data[out] = clamp(r + (tr - r) * strength); output.data[out + 1] = clamp(g + (tg - g) * strength); output.data[out + 2] = clamp(b + (tb - b) * strength); output.data[out + 3] = sourceData.data[p + 3] }
  cache.set(key, output); return output
}
self.onmessage = async ({ data }: MessageEvent<TintRequest | { type: 'background'; revision: string; bitmap: ImageBitmap } | { type: 'clear' }>) => { try { if (data.type === 'clear') { cache.clear(); source?.close(); source = undefined; sourceRevision = ''; return } if (data.type === 'background') { cache.clear(); source?.close(); source = data.bitmap; sourceRevision = data.revision; return } const result = render(data); const canvas = new OffscreenCanvas(result.width, result.height); canvas.getContext('2d')!.putImageData(result, 0, 0); const bitmap = canvas.transferToImageBitmap(); self.postMessage({ type: 'rendered', id: data.id, bitmap }, [bitmap]) } catch (error) { self.postMessage({ type: 'failed', id: (data as TintRequest).id, code: error instanceof Error ? error.message : 'BackgroundTintFailed' }) } }
