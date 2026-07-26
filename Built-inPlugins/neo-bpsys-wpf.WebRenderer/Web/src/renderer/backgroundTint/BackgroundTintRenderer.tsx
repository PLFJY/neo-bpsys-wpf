import { useEffect, useMemo, useRef, useState } from 'react'
import type { BackgroundTintConfig } from '../controlTypes'
import type { RuntimeState } from '../../protocol/runtime'
import type { WebRenderContext } from '../WebRenderContext'
import { normalizePolygonPoints, polygonClipPath } from './normalizedPolygon'

type Pending = { resolve: (value: ImageBitmap) => void; reject: (reason: unknown) => void }
type TintRequest = { type: 'render'; revision: string; width: number; height: number; canvasWidth: number; canvasHeight: number; left: number; top: number; tint: string; mode: number; tintStrength: number; textureStrength: number; points?: { x: number; y: number }[] }
class TintWorker {
  private worker = new Worker(new URL('./BackgroundTintWorker.ts', import.meta.url), { type: 'module' })
  private revision?: string; private next = 1; private pending = new Map<number, Pending>()
  constructor() { this.worker.onmessage = ({ data }) => { if (data.type === 'rendered') { const item = this.pending.get(data.id); this.pending.delete(data.id); item?.resolve(data.bitmap) } else if (data.type === 'failed') { const item = this.pending.get(data.id); this.pending.delete(data.id); item?.reject(new Error(data.code)) } } }
  async prepare(url: string | undefined, revision: string | undefined) { if (!url || !revision || this.revision === revision) return; const response = await fetch(url); if (!response.ok) throw new Error('BackgroundFetchFailed'); const bitmap = await createImageBitmap(await response.blob()); this.worker.postMessage({ type: 'background', revision, bitmap }, [bitmap]); this.revision = revision }
  render(value: TintRequest) { const id = this.next++; return new Promise<ImageBitmap>((resolve, reject) => { this.pending.set(id, { resolve, reject }); this.worker.postMessage({ ...value, id }) }) }
}
let tintWorker: TintWorker | undefined
const getTintWorker = () => tintWorker ??= new TintWorker()
const diagnosed = new Set<string>()
const number = (value: unknown, fallback = 0) => typeof value === 'number' && Number.isFinite(value) ? value : fallback
const mode = (value: unknown) => typeof value === 'number' ? value : value === 'Multiply' ? 0 : value === 'BaseColorWithTexture' ? 2 : 1
export function BackgroundTintRenderer({ config, runtime, context }: { config: BackgroundTintConfig; runtime: RuntimeState; context: WebRenderContext }) {
  const canvas = useRef<HTMLCanvasElement>(null); const latest = useRef(0); const [failed, setFailed] = useState(false); const [animated, setAnimated] = useState<Record<string, unknown>>({})
  const points = useMemo(() => config.ControlType === 'BackgroundTintPolygon' ? normalizePolygonPoints(config.Points) : null, [config.ControlType, config.Points])
  const bound = config.TintBindingPath ? runtime.values[config.TintBindingPath] : undefined; const tint = typeof animated.TintColor === 'string' ? animated.TintColor : typeof bound === 'string' ? bound : config.TintColor ?? '#FFFFFFFF'; const left = number(config.Left); const top = number(config.Top); const width = Math.max(1, number(config.Width, context.canvasWidth)); const height = Math.max(1, number(config.Height, context.canvasHeight))
  const tintStrength = number(animated.TintStrength, number(config.TintStrength, 1)); const textureStrength = number(animated.TextureStrength, number(config.TextureStrength, .45))
  useEffect(() => { const root = canvas.current?.closest<HTMLElement>('[data-control-root]'); const onTint = (event: Event) => { const detail = (event as CustomEvent<{ property: string; value: unknown }>).detail; if (detail?.property) setAnimated(value => ({ ...value, [detail.property]: detail.value })) }; root?.addEventListener('web-renderer:tint-state-changed', onTint); return () => root?.removeEventListener('web-renderer:tint-state-changed', onTint) }, [])
  useEffect(() => { const id = ++latest.current; setFailed(false); void (async () => { try { const worker = getTintWorker(); await worker.prepare(context.backgroundUrl, context.backgroundRevision); if (!context.backgroundRevision) return; const bitmap = await worker.render({ type: 'render', revision: context.backgroundRevision, width, height, canvasWidth: context.canvasWidth, canvasHeight: context.canvasHeight, left, top, tint, mode: mode(config.TintMode), tintStrength, textureStrength, points: config.ControlType === 'BackgroundTintPolygon' ? (points ?? []) : undefined }); if (latest.current !== id) { bitmap.close(); return } const target = canvas.current; if (target) { target.width = bitmap.width; target.height = bitmap.height; const renderer = target.getContext('bitmaprenderer'); if (renderer) renderer.transferFromImageBitmap(bitmap); else { target.getContext('2d')?.drawImage(bitmap, 0, 0); bitmap.close() } } } catch (error) { if (latest.current === id) { setFailed(true); console.warn('[Web Renderer] BackgroundTint render failed.', error) } } })(); return () => { latest.current++ } }, [config.ControlType, config.Points, config.TintMode, context.backgroundRevision, context.backgroundUrl, context.canvasHeight, context.canvasWidth, height, left, points, tint, tintStrength, textureStrength, top, width])
  if (config.ControlType === 'BackgroundTintPolygon' && !points) {
    const diagnostic = 'BackgroundTintPolygon:invalid-polygon'
    if (!diagnosed.has(diagnostic)) { diagnosed.add(diagnostic); console.warn('[Web Renderer] Invalid BackgroundTintPolygon points.') }
  }
  return <canvas ref={canvas} data-background-tint={config.ControlType} data-background-tint-failed={failed || undefined} style={{ width: '100%', height: '100%', display: 'block', borderRadius: config.ControlType === 'BackgroundTintRectangle' ? `${number(config.RadiusY)}px / ${number(config.RadiusX)}px` : undefined, clipPath: config.ControlType === 'BackgroundTintPolygon' ? polygonClipPath(points) : undefined }} />
}
