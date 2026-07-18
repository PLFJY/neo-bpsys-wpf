import { CSSProperties, useEffect, useRef, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { BehaviorDocument, WebBehaviorRuntime } from './behaviorRuntime'
import './styles.css'

type AnyRecord = Record<string, unknown>
type Bootstrap = { FullWindowType: string; DisplayName: string; Layout: AnyRecord | null; BehaviorDocument?: BehaviorDocument | null; Resources: Record<string, string>; Diagnostics: string[] }
type RuntimeMessage = { type: string; payload?: { Generation?: number; Sequence?: number; Values?: Record<string, unknown> } }
type RuntimeState = { values: Record<string, unknown>; sequence: number; generation: number }
const finite = (value: unknown, fallback = 0) => typeof value === 'number' && Number.isFinite(value) ? value : fallback
const text = (value: unknown) => typeof value === 'string' ? value : undefined
const base64 = (value: string) => btoa(unescape(encodeURIComponent(value))).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '')

function color(value: unknown, fallback = 'transparent') {
  const source = text(value); if (!source) return fallback
  const match = /^#([0-9a-f]{8})$/i.exec(source)
  if (match) { const hex = match[1]; return `rgba(${parseInt(hex.slice(2, 4), 16)}, ${parseInt(hex.slice(4, 6), 16)}, ${parseInt(hex.slice(6, 8), 16)}, ${(parseInt(hex.slice(0, 2), 16) / 255).toFixed(4)})` }
  return /^#[0-9a-f]{6}$/i.test(source) ? source : fallback
}
function resource(resources: Record<string, string>, value: unknown) { return typeof value === 'string' ? resources[value] : undefined }
function fontFamily(value: unknown) { const raw = text(value); if (!raw) return undefined; const index = raw.lastIndexOf('#'); return index >= 0 ? raw.slice(index + 1) : raw }
function controlStyle(config: AnyRecord, runtime: RuntimeState): CSSProperties {
  const visibilityPath = text(config.VisibilityBindingPath) ?? text(config.BindingPath)
  const boundVisibility = visibilityPath ? runtime.values[visibilityPath] : undefined
  const hidden = config.Visibility === 'Collapsed' || config.Visibility === 'Hidden' || boundVisibility === false
  return { position: 'absolute', left: finite(config.Left), top: finite(config.Top), width: typeof config.Width === 'number' ? config.Width : undefined, height: typeof config.Height === 'number' ? config.Height : undefined, zIndex: finite(config.ZIndex), display: hidden ? 'none' : undefined, filter: config.IsGaussianBlurEnabled ? `blur(${finite(config.GaussianBlurRadius)}px)` : undefined }
}
function bindingText(config: AnyRecord, runtime: RuntimeState, key = 'Text') {
  const binding = config.TextBinding as AnyRecord | undefined; const sources = binding?.Sources as AnyRecord[] | undefined
  if (Array.isArray(sources) && sources.length > 0) {
    const values = sources.map(source => runtime.values[text(source.Path) ?? '']).map(value => value == null ? text(binding?.NullText) ?? '' : String(value))
    if (values.some((_, index) => runtime.values[text(sources[index].Path) ?? ''] === undefined) && text(binding?.FallbackText)) return text(binding?.FallbackText)!
    const format = text(binding?.StringFormat)
    return format ? format.replace(/\{(\d+)(?::[^}]*)?\}/g, (_, index) => values[Number(index)] ?? '') : values.join(text(binding?.JoinSeparator) ?? '')
  }
  const bound = text(config.BindingPath); if (bound && runtime.values[bound] !== undefined) return runtime.values[bound] == null ? '' : String(runtime.values[bound])
  return text(config[key]) ?? ''
}
function behaviorAttrs(name: string, config: AnyRecord) { return { 'data-control-name': name, 'data-behavior-guid': text(config.BehaviorGuid) ?? undefined } }
function ImageControl({ name, config, resources, runtime }: { name: string; config: AnyRecord; resources: Record<string, string>; runtime: RuntimeState }) {
  const path = text(config.BindingPath); const image = (path && typeof runtime.values[path] === 'string' ? String(runtime.values[path]) : undefined) ?? resource(resources, config.ImagePath); const fit = ({ Fill: 'fill', Uniform: 'contain', UniformToFill: 'cover', None: 'none' } as Record<string, string>)[text(config.Stretch) ?? ''] ?? 'fill'
  const style = { ...controlStyle(config, runtime), overflow: config.ClipToBounds || finite(config.CornerRadius) > 0 ? 'hidden' : 'visible', borderRadius: finite(config.CornerRadius) || undefined }
  const lockPath = text(config.LockVisibilityBindingPath); const lockValue = lockPath ? runtime.values[lockPath] : undefined; const lockVisible = config.Lockable === true && (lockPath ? (config.LockVisibleWhen === 'VisibleWhenFalse' ? lockValue === false : lockValue === true) : config.LockVisibleWhen === 'Always')
  return <div className="image-control" id={name} {...behaviorAttrs(name, config)} style={style}><div data-overlay-below />
    <div data-behavior-content>{image ? <img src={image} style={{ width: '100%', height: '100%', objectFit: fit as CSSProperties['objectFit'] }} /> : text(config.BindingPath) ? <div className="binding-image">[{text(config.BindingPath)}]</div> : null}</div>
    {lockVisible && <img id={`${name}LockOverlay`} className="overlay" src={resource(resources, config.LockImagePath) ?? '/assets/missing'} style={{ zIndex: finite(config.LockZIndexOffset, 1) }} />}
    {config.PickingBorderAvailable === true && <img id={text(config.PickingBorderName) ?? `${name}PickingBorder`} className="overlay picking-border" src={resource(resources, config.PickingBorderImagePath) ?? '/assets/missing'} style={{ zIndex: finite(config.PickingBorderZIndexOffset, 2) }} />}
  <div data-overlay-above /></div>
}
function TextControl({ name, config, localized, runtime }: { name: string; config: AnyRecord; localized: boolean; runtime: RuntimeState }) {
  const value = localized ? bindingText(config, runtime, 'FallbackText') || text(config.LocalizationKey) || '' : bindingText(config, runtime)
  const boundColor = text(config.ColorBindingPath); const foreground = boundColor && typeof runtime.values[boundColor] === 'string' ? color(runtime.values[boundColor], '#fff') : color(config.Color, '#fff')
  return <div className="text-control" {...behaviorAttrs(name, config)} style={{ ...controlStyle(config, runtime), color: foreground, fontFamily: fontFamily(config.FontFamily), fontWeight: text(config.FontWeight), fontSize: finite(config.FontSize) || undefined, textAlign: (text(config.TextAlignment)?.toLowerCase() ?? 'left') as CSSProperties['textAlign'], whiteSpace: text(config.TextWrapping) ? 'pre-wrap' : 'nowrap', justifyContent: ({ Center: 'center', Right: 'flex-end', Left: 'flex-start', Stretch: 'stretch' } as Record<string, string>)[text(config.HorizontalAlignment) ?? ''] ?? 'flex-start', alignItems: ({ Center: 'center', Bottom: 'flex-end', Top: 'flex-start', Stretch: 'stretch' } as Record<string, string>)[text(config.VerticalAlignment) ?? ''] ?? 'flex-start' }}><div data-overlay-below /><span data-behavior-content>{value}</span><div data-overlay-above /></div>
}
function Shape({ config, background, resources, runtime }: { config: AnyRecord; background?: string; resources: Record<string, string>; runtime: RuntimeState }) {
  const polygon = Array.isArray(config.Points) ? (config.Points as AnyRecord[]).map(point => `${finite(point.X) * 100}% ${finite(point.Y) * 100}%`).join(', ') : undefined
  const gradient = config.UseGradient || config.FillMode === 'Gradient' ? `linear-gradient(${finite(config.GradientAngle)}deg, ${color(config.GradientStartColor ?? config.FillColor, '#fff')}, ${color(config.GradientEndColor, 'transparent')})` : color(config.FillColor, 'transparent')
  const tint = text(config.ControlType)?.startsWith('BackgroundTint')
  return <div className={tint ? 'tint-shape' : 'shape'} style={{ ...controlStyle(config, runtime), background: tint ? color(config.TintColor, '#fff') : gradient, border: finite(config.StrokeThickness) > 0 ? `${finite(config.StrokeThickness)}px solid ${color(config.StrokeColor, 'transparent')}` : undefined, borderRadius: polygon ? undefined : `${Math.max(finite(config.RadiusX), finite(config.RadiusY))}px`, clipPath: polygon ? `polygon(${polygon})` : undefined, backgroundImage: tint && background ? `linear-gradient(${color(config.TintColor, '#fff')}, ${color(config.TintColor, '#fff')}), url(${background})` : undefined, backgroundBlendMode: tint ? 'color' : undefined, backgroundSize: tint ? 'var(--canvas-width) var(--canvas-height)' : undefined, backgroundPosition: tint ? `-${finite(config.Left)}px -${finite(config.Top)}px` : undefined }} />
}
function Unknown({ name, config, runtime }: { name: string; config: AnyRecord; runtime: RuntimeState }) { return <div className="diagnostic" style={controlStyle(config, runtime)}>{name}<small>{text(config.ControlType) ?? 'Unknown'}</small></div> }
function GameProgress({ config, runtime }: { config: AnyRecord; runtime: RuntimeState }) {
  const progress = runtime.values['CurrentGame.GameProgress']; const bo3 = runtime.values.IsBo3Mode === true
  const index = typeof progress === 'number' ? progress : -1
  const game = index < 0 ? 'FREE' : `GAME ${Math.floor(index / 2) + 1}${bo3 && index >= 6 ? ' OT' : ''}`
  const half = index < 0 ? '' : index % 2 === 0 ? 'FIRST HALF' : 'SECOND HALF'
  const mode = text(config.DisplayMode) ?? 'Inline'; const value = mode.includes('HalfOnly') ? half : mode.includes('GameOnly') ? game : `${game}${half ? ` ${half}` : ''}`
  return <div className="text-control game-progress" style={{ ...controlStyle(config, runtime), color: color(config.Color, '#fff'), fontFamily: fontFamily(config.FontFamily), fontWeight: text(config.FontWeight), fontSize: finite(config.FontSize) || undefined, background: color(config.BackgroundColor, 'transparent'), padding: `${finite(config.PaddingTop)}px ${finite(config.PaddingRight)}px ${finite(config.PaddingBottom)}px ${finite(config.PaddingLeft)}px`, writingMode: mode.startsWith('Vertical') ? 'vertical-rl' : undefined }}>{value}</div>
}
function MapName({ config, runtime }: { config: AnyRecord; runtime: RuntimeState }) {
  const path = text(config.BindingPath) ?? 'CurrentGame.PickedMap'; const value = runtime.values[path]
  return <div className="text-control" style={{ ...controlStyle(config, runtime), color: color(config.Color, '#fff'), fontFamily: fontFamily(config.FontFamily), fontWeight: text(config.FontWeight), fontSize: finite(config.FontSize) || undefined }}>{value == null ? text(config.EmptyText) ?? '' : String(value)}</div>
}
function TalentTrait({ config, runtime }: { config: AnyRecord; runtime: RuntimeState }) {
  const survivor = text(config.DisplayKind)?.startsWith('Survivor'); const player = survivor ? `CurrentGame.SurPlayerList[${finite(config.PlayerIndex)}]` : 'CurrentGame.HunPlayer'
  const names = survivor ? ['BorrowedTime', 'TideTurner', 'FlywheelEffect', 'KneeJerkReflex'] : ['TrumpCard', 'Detention', 'ConfinedSpace', 'Insolence']
  const active = names.filter(name => runtime.values[`${player}.Talent.${name}`] === true)
  const hidden = !survivor && config.RespectTraitVisibility !== false && runtime.values.IsTraitVisible === false
  return <div className="talent-trait" style={{ ...controlStyle(config, runtime), display: hidden ? 'none' : 'flex', gap: finite(config.IconGap), alignItems: 'center' }}>{active.map(name => <span key={name} className="talent-icon" style={{ width: finite(config.IconSize, 38), height: finite(config.IconSize, 38) }}>{name}</span>)}</div>
}
function MapV2({ config, runtime, resources }: { config: AnyRecord; runtime: RuntimeState; resources: Record<string, string> }) {
  const key = text(config.MapKey) ?? ''; const prefix = `CurrentGame.MapV2Dictionary['${key}']`; const parts = Array.isArray(config.InternalParts) ? config.InternalParts as AnyRecord[] : []
  const part = (kind: string) => parts.find(item => text(item.Part) === kind) ?? {}
  const partStyle = (kind: string): CSSProperties => { const item = part(kind); return { position: 'absolute', left: finite(item.X), top: finite(item.Y), width: finite(item.Width), height: finite(item.Height) } }
  const banned = runtime.values[`${prefix}.IsBanned`] === true; const picked = runtime.values[`${prefix}.IsPicked`] === true
  return <div className="map-v2" style={controlStyle(config, runtime)}>
    <div style={partStyle('TeamName')}>{String(runtime.values[`${prefix}.OperationTeam.Name`] ?? '')}</div>
    <div className="map-card" style={{ ...partStyle('MapCard'), borderColor: color(banned ? config.MapBorderBannedColor : config.MapBorderNormalColor, '#2B483B') }} />
    <div className="map-name" style={partStyle('MapName')}>{String(runtime.values[`${prefix}.MapName`] ?? '')}</div>
    {runtime.values[`${prefix}.IsCampVisible`] === true && <div style={partStyle('CampName')}>{String(runtime.values[`${prefix}.OperationTeam.Camp`] ?? '')}</div>}
    {picked && <div className="map-picking" style={{ ...partStyle('PickingBorder'), background: color(config.PickingBorderFillColor, 'transparent'), backgroundImage: resource(resources, config.PickingBorderImagePath) ? `url(${resource(resources, config.PickingBorderImagePath)})` : undefined }} />}
  </div>
}
function GlobalScoreRow({ config, runtime }: { config: AnyRecord; runtime: RuntimeState }) {
  const cells = Array.isArray(config.Cells) ? config.Cells as AnyRecord[] : []
  return <div className="global-score-row" style={controlStyle(config, runtime)}>{cells.map((cell, index) => <div key={index} className="score-cell" style={{ position: 'absolute', left: finite(cell.X), top: finite(cell.Y), width: finite(cell.Width), height: finite(cell.Height) }}>-</div>)}</div>
}
function BehaviorParts({ guid, parts, resources }: { guid: string; parts: AnyRecord[]; resources: Record<string, string> }) {
  return <>{parts.map((part, index) => {
    const style: CSSProperties = { position: 'absolute', left: finite(part.Left), top: finite(part.Top), width: text(part.WidthText) === '100%' ? '100%' : finite(part.Width) || undefined, height: text(part.HeightText) === '100%' ? '100%' : finite(part.Height) || undefined, opacity: finite(part.Opacity, 1), visibility: text(part.Visibility)?.toLowerCase() === 'visible' ? 'visible' : 'hidden', zIndex: finite(part.ZIndex), background: color(part.Fill, 'transparent'), border: finite(part.StrokeThickness) ? `${finite(part.StrokeThickness)}px solid ${color(part.Stroke, 'transparent')}` : undefined }
    const image = resource(resources, part.ImagePath)
    return <div key={`${guid}:${index}`} data-animation-part={text(part.Name) ?? `part${index}`} style={style}>{text(part.Kind) === 'Image' && image ? <img src={image} style={{ width: '100%', height: '100%' }} /> : null}</div>
  })}</>
}
function Canvas({ bootstrap, runtime }: { bootstrap: Bootstrap; runtime: RuntimeState }) {
  const [viewport, setViewport] = useState(() => ({ width: window.innerWidth, height: window.innerHeight }))
  useEffect(() => { const update = () => setViewport({ width: window.innerWidth, height: window.innerHeight }); addEventListener('resize', update); return () => removeEventListener('resize', update) }, [])
  const layout = bootstrap.Layout!; const canvas = layout.CanvasSettings as AnyRecord; const controlLayout = layout.ControlLayout as AnyRecord; const controls = (controlLayout.Controls ?? {}) as Record<string, AnyRecord>; const background = resource(bootstrap.Resources, canvas.BackgroundImage)
  const windowSettings = layout.WindowSettings as AnyRecord; const width = finite(canvas.CanvasWidth, 1440); const height = finite(canvas.CanvasHeight, 810); const sx = viewport.width / width; const sy = viewport.height / height; const stretch = text(windowSettings.ViewboxStretch) ?? 'Fill'; const scaleX = stretch === 'None' ? 1 : stretch === 'Fill' ? sx : stretch === 'UniformToFill' ? Math.max(sx, sy) : Math.min(sx, sy); const scaleY = stretch === 'Fill' ? sy : scaleX
  const fontFaces = Object.entries(bootstrap.Resources).filter(([key]) => key.includes('/fonts/') || key.includes('Assets/Fonts')).map(([key, url]) => `@font-face{font-family:"${fontFamily(key)}";src:url("${url}");font-display:block;}`).join('\n')
  return <><style>{fontFaces}</style><div className="viewport"><div className="canvas" style={{ width, height, transform: `scale(${scaleX}, ${scaleY})`, ['--canvas-width' as string]: `${width}px`, ['--canvas-height' as string]: `${height}px`, backgroundImage: background ? `url(${background})` : undefined }}>
    {Object.entries(controls).map(([name, config]) => { const type = text(config.ControlType); const parts = ((bootstrap.BehaviorDocument?.ControlBehaviorSets ?? []).find(item => item.BehaviorGuid === text(config.BehaviorGuid))?.AnimationParts ?? []) as AnyRecord[]; const content = type === 'Text' ? <TextControl name={name} config={config} runtime={runtime} localized={false} /> : type === 'LocalizedText' ? <TextControl name={name} config={config} runtime={runtime} localized /> : type === 'Image' || type === 'BorderedImage' ? <ImageControl name={name} config={config} resources={bootstrap.Resources} runtime={runtime} /> : type === 'Rectangle' || type === 'Polygon' || type === 'BackgroundTintRectangle' || type === 'BackgroundTintPolygon' ? <Shape config={config} background={background} resources={bootstrap.Resources} runtime={runtime} /> : type === 'GameProgressText' ? <GameProgress config={config} runtime={runtime} /> : type === 'MapNameText' ? <MapName config={config} runtime={runtime} /> : type === 'TalentTraitDisplay' ? <TalentTrait config={config} runtime={runtime} /> : type === 'MapV2Display' ? <MapV2 config={config} runtime={runtime} resources={bootstrap.Resources} /> : type === 'GlobalScoreRow' ? <GlobalScoreRow config={config} runtime={runtime} /> : <Unknown name={name} config={config} runtime={runtime} />; return <div key={name} style={{ position: 'absolute', inset: 0, pointerEvents: 'none' }}>{content}{text(config.BehaviorGuid) ? <BehaviorParts guid={text(config.BehaviorGuid)!} parts={parts} resources={bootstrap.Resources} /> : null}</div> })}
  </div></div></>
}
function App() {
  const encoded = location.pathname.startsWith('/render/') ? location.pathname.slice('/render/'.length) : null
  const [bootstrap, setBootstrap] = useState<Bootstrap | null>(null); const [error, setError] = useState<string | null>(null); const [windows, setWindows] = useState<AnyRecord[]>([])
  const [runtime, setRuntime] = useState<RuntimeState>({ values: {}, sequence: 0, generation: 0 })
  const behaviorRuntime = useRef(new WebBehaviorRuntime())
  const load = () => { if (!encoded) return fetch('/api/windows').then(response => response.json()).then(setWindows).catch(() => setError('无法读取窗口列表。')); fetch(`/api/bootstrap/${encoded}`).then(async response => response.ok ? response.json() : Promise.reject(await response.json())).then(value => { if (!value || typeof value !== 'object' || !('Layout' in value)) throw new Error('Bootstrap schema is invalid.'); setBootstrap(value as Bootstrap); setError(null) }).catch(() => setError('无法加载或验证布局 bootstrap。')) }
  useEffect(() => {
    load(); const scheme = location.protocol === 'https:' ? 'wss' : 'ws'; let retry: number | undefined; let closed = false
    const connect = () => {
      const socket = new WebSocket(`${scheme}://${location.host}/ws`)
      socket.onmessage = event => { try {
        const message = JSON.parse(event.data) as RuntimeMessage
        if (message.type === 'bootstrap.changed') { behaviorRuntime.current.dispose(); setRuntime({ values: {}, sequence: 0, generation: 0 }); load(); return }
        if (message.type === 'behavior.event' && message.payload) { behaviorRuntime.current.publish(message.payload as unknown as import('./behaviorRuntime').BehaviorEvent); return }
        const payload = message.payload; if (!payload || typeof payload.Sequence !== 'number' || payload.Sequence <= runtime.sequence) return
        if (message.type === 'snapshot') setRuntime({ values: payload.Values ?? {}, sequence: payload.Sequence, generation: payload.Generation ?? 0 })
        if (message.type === 'bindingPatch') setRuntime(previous => payload.Sequence! <= previous.sequence ? previous : { values: { ...previous.values, ...(payload.Values ?? {}) }, sequence: payload.Sequence!, generation: payload.Generation ?? previous.generation })
      } catch { setError('收到无效的实时状态消息。') } }
      socket.onclose = () => { if (!closed) retry = window.setTimeout(connect, 1000) }
      return socket
    }
    const socket = connect(); return () => { closed = true; behaviorRuntime.current.dispose(); if (retry) clearTimeout(retry); socket.close() }
  // Runtime connections are intentionally centralized here; controls only consume RuntimeState.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [encoded])
  if (!encoded) return <main className="window-index"><h1>Web Renderer</h1>{windows.map(window => <a key={String(window.fullWindowType)} href={`/render/${base64(String(window.fullWindowType))}`}>{String(window.displayName)}</a>)}</main>
  if (error || !bootstrap?.Layout) return <main className="error-page"><h1>布局无法渲染</h1><p>{error ?? bootstrap?.Diagnostics.join('\n') ?? 'LayoutMissing'}</p></main>
  useEffect(() => { behaviorRuntime.current.replace(bootstrap?.BehaviorDocument as BehaviorDocument | undefined) }, [bootstrap])
  return <Canvas bootstrap={bootstrap} runtime={runtime} />
}
createRoot(document.getElementById('root')!).render(<App />)
