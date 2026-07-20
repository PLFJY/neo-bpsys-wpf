/// <reference lib="webworker" />

type Point = { X?: number; Y?: number }
type TintRequest = { type: 'render'; id: number; revision: string; width: number; height: number; canvasWidth: number; canvasHeight: number; left: number; top: number; tint: string; mode: number; tintStrength: number; textureStrength: number; points?: Point[] }
let source: ImageBitmap | undefined
let sourceRevision = ''
const cache = new Map<string, ImageData>()

function color(value: string) { const hex = /^#?([\da-f]{6}|[\da-f]{8})$/i.exec(value)?.[1] ?? 'ffffff'; const offset = hex.length === 8 ? 2 : 0; return [parseInt(hex.slice(offset, offset + 2), 16), parseInt(hex.slice(offset + 2, offset + 4), 16), parseInt(hex.slice(offset + 4, offset + 6), 16)] }
function inside(x: number, y: number, points?: Point[]) { if (!points?.length) return true; let contained = false; for (let i = 0, j = points.length - 1; i < points.length; j = i++) { const a = points[i], b = points[j]; const ay = a.Y ?? 0, by = b.Y ?? 0; if ((ay > y) !== (by > y) && x < ((b.X ?? 0) - (a.X ?? 0)) * (y - ay) / (by - ay) + (a.X ?? 0)) contained = !contained } return contained }
function luminance(r: number, g: number, b: number) { return r * .2126 + g * .7152 + b * .0722 }
function clamp(v: number) { return Math.max(0, Math.min(255, Math.round(v))) }
function render(request: TintRequest) {
  if (!source || sourceRevision !== request.revision) throw new Error('BackgroundUnavailable')
  const key = JSON.stringify(request)
  const cached = cache.get(key)
  if (cached) return cached
  const left = Math.max(0, Math.floor(request.left)), top = Math.max(0, Math.floor(request.top)); const width = Math.max(1, Math.floor(request.width)), height = Math.max(1, Math.floor(request.height))
  const input = new OffscreenCanvas(source.width, source.height); const inputContext = input.getContext('2d', { willReadFrequently: true })!; inputContext.drawImage(source, 0, 0)
  const sourceData = inputContext.getImageData(0, 0, source.width, source.height); const output = new ImageData(width, height); const tint = color(request.tint); const ts = Math.max(0, Math.min(1, request.tintStrength)); const texture = Math.max(0, Math.min(1, request.textureStrength));
  let total = 0, count = 0
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) { const cx = left + x + .5, cy = top + y + .5; if (!inside(cx, cy, request.points)) continue; const sx = Math.min(source.width - 1, Math.max(0, Math.floor(cx * source.width / request.canvasWidth))), sy = Math.min(source.height - 1, Math.max(0, Math.floor(cy * source.height / request.canvasHeight))); const p = (sy * source.width + sx) * 4; if (sourceData.data[p + 3]) { total += luminance(sourceData.data[p], sourceData.data[p + 1], sourceData.data[p + 2]); count++ } }
  const average = count ? total / count : 0
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) { const cx = left + x + .5, cy = top + y + .5, out = (y * width + x) * 4; if (!inside(cx, cy, request.points)) continue; const sx = Math.min(source.width - 1, Math.max(0, Math.floor(cx * source.width / request.canvasWidth))), sy = Math.min(source.height - 1, Math.max(0, Math.floor(cy * source.height / request.canvasHeight))), p = (sy * source.width + sx) * 4; const r = sourceData.data[p], g = sourceData.data[p + 1], b = sourceData.data[p + 2]; const l = luminance(r, g, b); let tr: number, tg: number, tb: number; if (request.mode === 0) { tr = r * tint[0] / 255; tg = g * tint[1] / 255; tb = b * tint[2] / 255 } else if (request.mode === 2) { const detail = (l - average) * texture; tr = tint[0] + detail; tg = tint[1] + detail; tb = tint[2] + detail } else { tr = l * tint[0] / 255; tg = l * tint[1] / 255; tb = l * tint[2] / 255 } output.data[out] = clamp(r + (tr - r) * ts); output.data[out + 1] = clamp(g + (tg - g) * ts); output.data[out + 2] = clamp(b + (tb - b) * ts); output.data[out + 3] = sourceData.data[p + 3] }
  cache.set(key, output); return output
}
self.onmessage = async ({ data }: MessageEvent<TintRequest | { type: 'background'; revision: string; bitmap: ImageBitmap } | { type: 'clear' }>) => { try { if (data.type === 'clear') { cache.clear(); source?.close(); source = undefined; sourceRevision = ''; return } if (data.type === 'background') { cache.clear(); source?.close(); source = data.bitmap; sourceRevision = data.revision; return } const dataOut = render(data); const canvas = new OffscreenCanvas(dataOut.width, dataOut.height); canvas.getContext('2d')!.putImageData(dataOut, 0, 0); const bitmap = canvas.transferToImageBitmap(); self.postMessage({ type: 'rendered', id: data.id, bitmap }, [bitmap]) } catch (error) { self.postMessage({ type: 'failed', id: (data as TintRequest).id, code: error instanceof Error ? error.message : 'BackgroundTintFailed' }) } }
