export type RecordValue = Record<string, unknown>
export type BehaviorEvent = { EventType: string; WindowType?: string; CanvasName?: string; Source?: string; Payload: RecordValue }
type Node = { NodeId: string; NodeType: string; Properties?: RecordValue }
type Edge = { SourceNodeId: string; SourcePort: string; TargetNodeId: string; TargetPort: string }
type Graph = { Nodes?: Node[]; Connections?: Edge[] }
type Behavior = { BehaviorId: string; Name?: string; Enabled?: boolean; Kind?: string; Trigger?: Trigger; StartTrigger?: Trigger; StopTriggers?: Trigger[]; Graph?: Graph; StartGraph?: Graph; LoopGraph?: Graph; StopGraph?: Graph; ExitGraph?: Graph; EnterGraph?: Graph; TransitionTrigger?: Trigger; ReentryPolicy?: string; LoopPolicy?: RecordValue }
type Trigger = { EventType?: string; Filters?: { Left?: string; Operator?: string; Right?: string }[] }
type ControlSet = { BehaviorGuid: string; DisplayName?: string; AnimationParts?: Part[]; Behaviors?: Behavior[] }
export type BehaviorDocument = { ControlBehaviorSets?: ControlSet[] }
type Part = RecordValue & { Name?: string; Layer?: string; Kind?: string; ImagePath?: string }
type Context = { event: BehaviorEvent; start?: BehaviorEvent; stop?: BehaviorEvent; guid: string; display?: string }

const supported = new Set(['Opacity', 'Visibility', 'VisualOffsetX', 'VisualOffsetY', 'ClipInsetLeft', 'ClipInsetTop', 'ClipInsetRight', 'ClipInsetBottom', 'ScaleX', 'ScaleY', 'Rotation', 'Width', 'Height', 'FillColor', 'StrokeColor', 'StrokeThickness', 'TextColor', 'Foreground', 'FontSize', 'TintColor', 'TintStrength', 'TextureStrength', 'GaussianBlurRadius'])
const number = (value: unknown) => typeof value === 'number' && Number.isFinite(value) ? value : typeof value === 'string' && value.trim() !== '' && Number.isFinite(Number(value)) ? Number(value) : undefined
const key = (path: string, payload: RecordValue) => payload[path] ?? payload[`Event.${path}`]
const resolve = (path: unknown, context: Context): unknown => {
  if (typeof path !== 'string') return path
  const find = (prefix: string, value: BehaviorEvent | undefined) => path.startsWith(prefix) ? key(path.slice(prefix.length), value?.Payload ?? {}) : undefined
  const event = find('Event.', context.event); if (event !== undefined) return event
  const start = find('StartEvent.', context.start); if (start !== undefined) return start
  const stop = find('StopEvent.', context.stop); if (stop !== undefined) return stop
  if (path === 'Context.TriggerEventType') return context.event.EventType
  if (path === 'Context.CurrentControlDisplayName') return context.display
  if (path === 'Context.BehaviorGuid') return context.guid
  return path
}
const compare = (left: unknown, op = 'Equals', right?: unknown) => {
  const a = left == null ? '' : String(left); const b = right == null ? '' : String(right)
  const an = number(a); const bn = number(b); const order = an !== undefined && bn !== undefined ? an - bn : a.localeCompare(b, undefined, { sensitivity: 'accent' })
  switch (op) { case 'Equals': return a.toLowerCase() === b.toLowerCase(); case 'NotEquals': return a.toLowerCase() !== b.toLowerCase(); case 'Contains': return a.toLowerCase().includes(b.toLowerCase()); case 'NotContains': return !a.toLowerCase().includes(b.toLowerCase()); case 'GreaterThan': return order > 0; case 'GreaterThanOrEqual': return order >= 0; case 'LessThan': return order < 0; case 'LessThanOrEqual': return order <= 0; case 'Exists': return left != null; default: return false }
}
const trigger = (descriptor: Trigger | undefined, event: BehaviorEvent) => !!descriptor && descriptor.EventType === event.EventType && (descriptor.Filters ?? []).every(filter => compare(resolve(filter.Left, { event, guid: '' }), filter.Operator, filter.Right))

/** Resolves stable behavior targets without exposing arbitrary selectors. */
export class WebAnimationTargetResolver {
  resolve(target: unknown, self: string, layer: unknown): HTMLElement | null {
    const text = typeof target === 'string' ? target.trim() : 'Self'; let guid = self; let part: string | undefined; let name: string | undefined
    if (text.startsWith('part:')) { const [, id, ...rest] = text.split(':'); guid = id; part = rest.join(':') }
    else if (text.startsWith('guid:')) guid = text.slice(5)
    else if (text.startsWith('name:')) name = text.slice(5)
    else if (text !== 'Self' && /^[0-9a-f-]{36}$/i.test(text)) guid = text
    else if (text !== 'Self') name = text
    const host = name ? document.querySelector<HTMLElement>(`[data-control-name="${CSS.escape(name)}"]`) : document.querySelector<HTMLElement>(`[data-behavior-guid="${CSS.escape(guid)}"]`)
    if (!host) return null
    if (part) return host.querySelector<HTMLElement>(`[data-animation-part="${CSS.escape(part)}"]`)
    const root = host.querySelector<HTMLElement>(':scope > [data-control-root]') ?? host
    const effective = String(layer ?? 'Auto'); if (effective === 'Content') return root.querySelector<HTMLElement>('[data-behavior-content]') ?? root
    if (effective === 'OverlayAbove') return root.querySelector<HTMLElement>('[data-overlay-above]') ?? root
    if (effective === 'OverlayBelow') return root.querySelector<HTMLElement>('[data-overlay-below]') ?? root
    return root
  }
}

/** Applies the phase-4 animatable property set and owns active WAAPI animations. */
export class WebAnimatablePropertyAdapterRegistry {
  private readonly base = new WeakMap<HTMLElement, RecordValue>(); private readonly animations = new Set<Animation>()
  private capture(element: HTMLElement, property: string) { let values = this.base.get(element); if (!values) { values = {}; this.base.set(element, values) }; if (!(property in values)) values[property] = this.read(element, property); return values }
  private read(element: HTMLElement, property: string): unknown { const style = getComputedStyle(element); if (property === 'Opacity') return style.opacity; if (property === 'Visibility') return style.display === 'none' ? 'Collapsed' : style.visibility === 'hidden' ? 'Hidden' : 'Visible'; if (property === 'Width') return element.style.width || `${element.getBoundingClientRect().width}px`; if (property === 'Height') return element.style.height || `${element.getBoundingClientRect().height}px`; return element.dataset[`web${property}`] ?? (property.startsWith('Scale') ? '1' : '0') }
  set(element: HTMLElement, property: string, value: unknown): boolean {
    if (!supported.has(property)) return false; this.capture(element, property); const raw = String(value ?? '')
    if (property === 'Opacity') element.style.opacity = raw
    else if (property === 'Visibility') { element.style.visibility = raw.toLowerCase() === 'hidden' ? 'hidden' : 'visible'; element.style.display = raw.toLowerCase() === 'collapsed' ? 'none' : '' }
    else if (property === 'Width' || property === 'Height') element.style[property.toLowerCase() as 'width' | 'height'] = raw === 'Auto' ? 'auto' : `${number(raw) ?? raw}px`
    else if (property === 'FillColor') element.style.backgroundColor = raw
    else if (property === 'StrokeColor') element.style.borderColor = raw
    else if (property === 'StrokeThickness') element.style.borderWidth = `${number(raw) ?? 0}px`
    else if (property === 'TextColor' || property === 'Foreground') element.style.color = raw
    else if (property === 'FontSize') element.style.fontSize = `${number(raw) ?? raw}px`
    else { element.dataset[`web${property}`] = raw; this.applyVisualState(element) }
    return true
  }
  reset(element: HTMLElement, property: string) { const values = this.base.get(element); for (const item of property === 'All' ? Object.keys(values ?? {}) : [property]) if (values && item in values) this.set(element, item, values[item]); if (property === 'All') this.base.delete(element) }
  async animate(element: HTMLElement, property: string, from: unknown, to: unknown, duration: unknown, wait: boolean, signal: AbortSignal, easing = 'Linear'): Promise<boolean> {
    if (!supported.has(property)) return false; this.set(element, property, from); const milliseconds = Math.max(0, number(duration) ?? 0); if (property === 'Visibility' || milliseconds === 0) { this.set(element, property, to); return true }
    const keyframes = this.keyframes(property, from, to); if (!keyframes) return false
    const animation = element.animate(keyframes, { duration: milliseconds, fill: 'forwards', easing: this.easing(easing) }); this.animations.add(animation)
    const cancel = () => animation.cancel(); signal.addEventListener('abort', cancel, { once: true }); const done = animation.finished.catch(() => undefined).then(() => { this.animations.delete(animation); signal.removeEventListener('abort', cancel); if (!signal.aborted) this.set(element, property, to) }); if (wait) await done; return true
  }
  cancelAll() { for (const animation of this.animations) animation.cancel(); this.animations.clear() }
  private keyframes(property: string, from: unknown, to: unknown): Keyframe[] | null {
    if (property === 'Opacity') return [{ opacity: String(from) }, { opacity: String(to) }]
    if (['Width', 'Height', 'FillColor', 'StrokeColor', 'StrokeThickness', 'TextColor', 'Foreground', 'FontSize'].includes(property)) { const css = property === 'FillColor' ? 'backgroundColor' : property === 'StrokeColor' ? 'borderColor' : property === 'StrokeThickness' ? 'borderWidth' : property === 'TextColor' || property === 'Foreground' ? 'color' : property.toLowerCase(); return [{ [css]: String(from) }, { [css]: String(to) }] }
    const before = this.stateTransform(property, from); const after = this.stateTransform(property, to); return [{ transform: before }, { transform: after }]
  }
  private applyVisualState(element: HTMLElement) { const d = element.dataset; element.style.transform = `translate(${d.webVisualOffsetX ?? 0}px,${d.webVisualOffsetY ?? 0}px) scale(${d.webScaleX ?? 1},${d.webScaleY ?? 1}) rotate(${d.webRotation ?? 0}deg)`; const clip = [d.webClipInsetTop ?? 0, d.webClipInsetRight ?? 0, d.webClipInsetBottom ?? 0, d.webClipInsetLeft ?? 0].map(value => `${value}px`).join(' '); element.style.clipPath = `inset(${clip})`; element.style.filter = `blur(${d.webGaussianBlurRadius ?? 0}px)`; element.style.setProperty('--web-tint-color', d.webTintColor ?? 'transparent'); element.style.setProperty('--web-tint-strength', d.webTintStrength ?? '1'); element.style.setProperty('--web-texture-strength', d.webTextureStrength ?? '1') }
  private stateTransform(property: string, value: unknown) { const state: Record<string, string> = { VisualOffsetX: '0', VisualOffsetY: '0', ScaleX: '1', ScaleY: '1', Rotation: '0' }; state[property] = String(value); return `translate(${state.VisualOffsetX}px,${state.VisualOffsetY}px) scale(${state.ScaleX},${state.ScaleY}) rotate(${state.Rotation}deg)` }
  private easing(name: string) { return ({ Linear: 'linear', SineInOut: 'cubic-bezier(.445,.05,.55,.95)', CubicOut: 'cubic-bezier(.215,.61,.355,1)', CubicIn: 'cubic-bezier(.55,.055,.675,.19)', CubicInOut: 'cubic-bezier(.645,.045,.355,1)', BackOut: 'cubic-bezier(.175,.885,.32,1.275)' } as Record<string, string>)[name] ?? 'linear' }
}

/** Executes the persisted v3 behavior graph in a single Web page. */
export class WebBehaviorRuntime {
  private document: BehaviorDocument | undefined; private readonly resolver = new WebAnimationTargetResolver(); private readonly adapters = new WebAnimatablePropertyAdapterRegistry(); private readonly active = new Map<string, AbortController[]>(); private readonly loops = new Map<string, AbortController>(); private readonly transitions = new Map<string, { controller: AbortController; entries: { set: ControlSet; behavior: Behavior; context: Context }[] }>();
  constructor(private readonly warn: (message: string) => void = console.warn) {}
  replace(document: BehaviorDocument | undefined) { this.dispose(); this.document = document }
  dispose() { for (const controllers of this.active.values()) controllers.forEach(controller => controller.abort()); this.active.clear(); for (const controller of this.loops.values()) controller.abort(); this.loops.clear(); for (const transition of this.transitions.values()) transition.controller.abort(); this.transitions.clear(); this.adapters.cancelAll() }
  /** Executes matching transition exits and resolves only after all exits settle. */
  async prepareTransition(payload: { correlationId?: string; requests?: RecordValue[] }) {
    const correlationId = payload.correlationId; if (!correlationId || this.transitions.has(correlationId)) return false; const controller = new AbortController(); const entries: { set: ControlSet; behavior: Behavior; context: Context }[] = []
    for (const request of payload.requests ?? []) for (const set of this.document?.ControlBehaviorSets ?? []) {
      if (String(request.TargetBehaviorGuid ?? request.targetBehaviorGuid ?? '').toLowerCase() !== set.BehaviorGuid.toLowerCase()) continue
      for (const behavior of set.Behaviors ?? []) if (behavior.Enabled !== false && behavior.Kind === 'Transition') {
        const event: BehaviorEvent = { EventType: String(request.TransitionType ?? request.transitionType ?? ''), Payload: (request.Payload ?? request.payload ?? {}) as RecordValue }
        if (trigger(behavior.TransitionTrigger, event)) entries.push({ set, behavior, context: { event, guid: set.BehaviorGuid, display: set.DisplayName } })
      }
    }
    this.transitions.set(correlationId, { controller, entries }); await Promise.all(entries.map(entry => this.execute((entry.behavior as Behavior & { ExitGraph?: Graph }).ExitGraph, entry.context, controller.signal).catch(() => undefined))); return !controller.signal.aborted
  }
  /** Starts matching transition enters after the host has committed state. */
  async commitTransition(correlationId: string) { const transition = this.transitions.get(correlationId); if (!transition) return false; await Promise.all(transition.entries.map(entry => this.execute((entry.behavior as Behavior & { EnterGraph?: Graph }).EnterGraph, entry.context, transition.controller.signal).catch(() => undefined))); this.transitions.delete(correlationId); return !transition.controller.signal.aborted }
  /** Cancels a prepared transition without performing browser-side business work. */
  cancelTransition(correlationId: string) { const transition = this.transitions.get(correlationId); if (!transition) return; transition.controller.abort(); this.transitions.delete(correlationId) }
  publish(event: BehaviorEvent) { for (const set of this.document?.ControlBehaviorSets ?? []) for (const behavior of set.Behaviors ?? []) if (behavior.Enabled !== false) this.dispatch(set, behavior, event) }
  private dispatch(set: ControlSet, behavior: Behavior, event: BehaviorEvent) { if (behavior.Kind === 'Transition') { if (trigger(behavior.TransitionTrigger, event)) this.warn(`TransitionDeferred:${behavior.BehaviorId}`); return } if (behavior.Kind === 'Loop') { if (trigger(behavior.StartTrigger, event)) this.startLoop(set, behavior, event); if ((behavior.StopTriggers ?? []).some(item => trigger(item, event))) this.stopLoop(set, behavior, event); return } if (trigger(behavior.Trigger, event)) this.start(set, behavior, behavior.Graph, { event, guid: set.BehaviorGuid, display: set.DisplayName }) }
  private start(set: ControlSet, behavior: Behavior, graph: Graph | undefined, context: Context) { const running = this.active.get(behavior.BehaviorId) ?? []; const policy = behavior.ReentryPolicy ?? 'InterruptPrevious'; if (policy === 'IgnoreIfRunning' && running.length) return; if (policy === 'InterruptPrevious') running.forEach(item => item.abort()); const controller = new AbortController(); this.active.set(behavior.BehaviorId, policy === 'AllowParallel' ? [...running, controller] : [controller]); const run = () => this.execute(graph, context, controller.signal).catch(error => !controller.signal.aborted && this.warn(String(error))).finally(() => { const values = this.active.get(behavior.BehaviorId) ?? []; this.active.set(behavior.BehaviorId, values.filter(item => item !== controller)) }); if (policy === 'Queue' && running.length) Promise.all(running.map(item => new Promise<void>(resolve => item.signal.addEventListener('abort', () => resolve(), { once: true })))).then(run); else run() }
  private startLoop(set: ControlSet, behavior: Behavior, event: BehaviorEvent) { if (this.loops.has(behavior.BehaviorId)) return; const controller = new AbortController(); this.loops.set(behavior.BehaviorId, controller); const context = { event, start: event, guid: set.BehaviorGuid, display: set.DisplayName }; void (async () => { await this.execute(behavior.StartGraph, context, controller.signal); const count = number(behavior.LoopPolicy?.RepeatCount) ?? -1; for (let index = 0; !controller.signal.aborted && (count < 0 || index < count); index++) { await this.execute(behavior.LoopGraph, context, controller.signal); const interval = Math.max(0, number(behavior.LoopPolicy?.IntervalMs) ?? 0); if (interval) await this.delay(interval, controller.signal) } })().catch(() => undefined).finally(() => this.loops.delete(behavior.BehaviorId)) }
  private stopLoop(set: ControlSet, behavior: Behavior, event: BehaviorEvent) { const controller = this.loops.get(behavior.BehaviorId); if (!controller) return; controller.abort(); if ((behavior.LoopPolicy?.StopMode ?? 'RunStopGraph') === 'RunStopGraph') { const stop = new AbortController(); void this.execute(behavior.StopGraph, { event, start: undefined, stop: event, guid: set.BehaviorGuid, display: set.DisplayName }, stop.signal) } }
  private async execute(graph: Graph | undefined, context: Context, signal: AbortSignal) { const nodes = new Map((graph?.Nodes ?? []).map(node => [node.NodeId, node])); const edges = graph?.Connections ?? []; const start = [...nodes.values()].find(node => node.NodeType === 'flow.start'); if (!start) return; let steps = 0; const flow = async (node: Node): Promise<void> => { if (signal.aborted || ++steps > 1000) return; const out = async (port: string) => { const edge = edges.find(item => item.SourceNodeId === node.NodeId && item.SourcePort === port); const next = edge && nodes.get(edge.TargetNodeId); if (next) await flow(next) }; const p = node.Properties ?? {}; switch (node.NodeType) { case 'flow.start': await out('Out'); break; case 'flow.end': break; case 'flow.delay': await this.delay(Math.max(0, number(p.DurationMs) ?? 0), signal); await out('Out'); break; case 'flow.parallel': await Promise.all(Array.from({ length: Math.min(20, Math.max(1, number(p.BranchCount) ?? 3)) }, (_, i) => out(`Branch${i + 1}`))); await out('Out'); break; case 'flow.if': await out(compare(resolve(p.Left, context), String(p.Operator ?? 'Equals'), resolve(p.Right, context)) ? 'True' : 'False'); break; case 'action.log': console.info('[WebBehavior]', p.Message ?? ''); await out('Out'); break; case 'action.setProperty': await this.action(node, nodes, edges, context, signal, 'set'); await out('Out'); break; case 'action.resetProperty': await this.action(node, nodes, edges, context, signal, 'reset'); await out('Out'); break; case 'action.animateProperty': await this.action(node, nodes, edges, context, signal, 'animate'); await out('Out'); break; default: if (!node.NodeType.startsWith('value.') && !node.NodeType.startsWith('math.')) this.warn(`UnknownNode:${node.NodeType}`) } }; await flow(start) }
  private async action(node: Node, nodes: Map<string, Node>, edges: Edge[], context: Context, signal: AbortSignal, kind: 'set' | 'reset' | 'animate') { const p = node.Properties ?? {}; const element = this.resolver.resolve(p.Target, context.guid, p.TargetLayer); const property = String(p.PropertyName ?? ''); if (!element) return this.warn(`TargetUnavailable:${String(p.Target)}`); if (kind === 'reset') { this.adapters.reset(element, property); return } const value = this.value(node, nodes, edges, kind === 'animate' ? 'ToInput' : 'ValueInput', kind === 'animate' ? p.To : p.Value, context, new Set()); if (value === undefined || !supported.has(property)) return this.warn(`UnsupportedWebProperty:${property}`); if (kind === 'set') this.adapters.set(element, property, value); else await this.adapters.animate(element, property, this.value(node, nodes, edges, 'FromInput', p.From, context, new Set()) ?? 0, value, p.DurationMs, p.WaitForCompletion !== false, signal, String(p.Easing ?? 'Linear')) }
  private value(node: Node, nodes: Map<string, Node>, edges: Edge[], port: string, literal: unknown, context: Context, visiting: Set<string>): unknown { const edge = edges.find(item => item.TargetNodeId === node.NodeId && item.TargetPort === port); if (!edge) return this.expression(literal, context); const source = nodes.get(edge.SourceNodeId); if (!source || visiting.has(source.NodeId)) return undefined; visiting.add(source.NodeId); const p = source.Properties ?? {}; const input = (name: string, fallback = 0) => number(this.value(source, nodes, edges, name, fallback, context, visiting)) ?? fallback; let result: number | undefined; switch (source.NodeType) { case 'value.number': result = number(p.Value); break; case 'value.eventContext': result = number(resolve(p.Path, context)) ?? number(p.FallbackValue); break; case 'math.add': result = input('Left') + input('Right'); break; case 'math.subtract': result = input('Left') - input('Right'); break; case 'math.multiply': result = input('Left') * input('Right'); break; case 'math.divide': { const b = input('Right'); result = b === 0 ? undefined : input('Left') / b; break } case 'math.modulo': { const b = input('Right'); result = b === 0 ? undefined : input('Left') % b; break } case 'math.negate': result = -input('Value'); break; case 'math.abs': result = Math.abs(input('Value')); break; case 'math.min': result = Math.min(input('Left'), input('Right')); break; case 'math.max': result = Math.max(input('Left'), input('Right')); break; case 'math.clamp': result = Math.min(input('Max'), Math.max(input('Min'), input('Value'))); break; case 'math.round': result = Math.round(input('Value')); break; case 'math.floor': result = Math.floor(input('Value')); break; case 'math.ceil': result = Math.ceil(input('Value')); break; default: result = undefined } visiting.delete(source.NodeId); return number(result) }
  private expression(value: unknown, context: Context): unknown { if (typeof value !== 'string' || !value.startsWith('=')) return value; const expression = value.slice(1).replace(/(Event|StartEvent|StopEvent)\.([A-Za-z0-9_]+)/g, (_, prefix, name) => String(number(resolve(`${prefix}.${name}`, context)) ?? 'NaN')); if (!/^[0-9+\-*/%().,\sA-Za-z]+$/.test(expression)) return undefined; try { const translated = expression.replace(/\b(clamp|min|max|abs|round|floor|ceil)\b/g, 'Math.$1').replace(/\bMath\.clamp\(/g, 'clamp('); const clamp = (v: number, min: number, max: number) => Math.min(max, Math.max(min, v)); const result = Function('Math', 'clamp', `return (${translated})`)(Math, clamp); return number(result) } catch { return undefined } }
  private delay(milliseconds: number, signal: AbortSignal) { return new Promise<void>((resolve, reject) => { const id = window.setTimeout(resolve, milliseconds); signal.addEventListener('abort', () => { clearTimeout(id); reject(new DOMException('Cancelled', 'AbortError')) }, { once: true }) }) }
}
