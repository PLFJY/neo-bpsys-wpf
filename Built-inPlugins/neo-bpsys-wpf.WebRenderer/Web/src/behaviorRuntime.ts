export type RecordValue = Record<string, unknown>
export type BehaviorEvent = { EventType: string; WindowType?: string; CanvasName?: string; Source?: string; Payload: RecordValue }
type Node = { NodeId: string; NodeType: string; Properties?: RecordValue }
type Edge = { SourceNodeId: string; SourcePort: string; TargetNodeId: string; TargetPort: string }
type Graph = { Nodes?: Node[]; Connections?: Edge[] }
type Behavior = { BehaviorId: string; Name?: string; Enabled?: boolean; Kind?: string; Trigger?: Trigger; StartTrigger?: Trigger; StopTriggers?: Trigger[]; Graph?: Graph; StartGraph?: Graph; LoopGraph?: Graph; StopGraph?: Graph; TransitionTrigger?: Trigger; ReentryPolicy?: string; LoopPolicy?: RecordValue }
type Trigger = { EventType?: string; Filters?: { Left?: string; Operator?: string; Right?: string }[] }
type ControlSet = { BehaviorGuid: string; DisplayName?: string; AnimationParts?: Part[]; Behaviors?: Behavior[] }
export type BehaviorDocument = { ControlBehaviorSets?: ControlSet[] }
type Part = RecordValue & { Name?: string; Layer?: string; Kind?: string; ImagePath?: string }
type Context = { event: BehaviorEvent; start?: BehaviorEvent; stop?: BehaviorEvent; guid: string; display?: string }

const supported = new Set(['Opacity', 'Visibility', 'VisualOffsetX', 'VisualOffsetY', 'ScaleX', 'ScaleY', 'Rotation'])
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
    const root = name ? document.querySelector<HTMLElement>(`[data-control-name="${CSS.escape(name)}"]`) : document.querySelector<HTMLElement>(`[data-behavior-guid="${CSS.escape(guid)}"]`)
    if (!root) return null
    if (part) return root.querySelector<HTMLElement>(`[data-animation-part="${CSS.escape(part)}"]`)
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
  private read(element: HTMLElement, property: string) { if (property === 'Opacity') return element.style.opacity || '1'; if (property === 'Visibility') return element.style.visibility || 'visible'; return element.dataset[`web${property}`] ?? (property.startsWith('Scale') ? '1' : '0') }
  set(element: HTMLElement, property: string, value: unknown): boolean { if (!supported.has(property)) return false; this.capture(element, property); if (property === 'Opacity') element.style.opacity = String(value); else if (property === 'Visibility') element.style.visibility = String(value).toLowerCase() === 'visible' ? 'visible' : 'hidden'; else { element.dataset[`web${property}`] = String(value); this.transform(element) } return true }
  reset(element: HTMLElement, property: string) { const values = this.base.get(element); for (const item of property === 'All' ? Object.keys(values ?? {}) : [property]) if (values && item in values) this.set(element, item, values[item]) }
  async animate(element: HTMLElement, property: string, from: unknown, to: unknown, duration: unknown, wait: boolean, signal: AbortSignal): Promise<boolean> {
    if (!supported.has(property)) return false; this.set(element, property, from); const milliseconds = Math.max(0, number(duration) ?? 0)
    if (property === 'Visibility') { this.set(element, property, to); return true }
    const before = this.css(property, from); const after = this.css(property, to); const animation = element.animate([before, after], { duration: milliseconds, fill: 'forwards', easing: 'linear' }); this.animations.add(animation)
    const cancel = () => animation.cancel(); signal.addEventListener('abort', cancel, { once: true })
    const done = animation.finished.catch(() => undefined).then(() => { this.animations.delete(animation); signal.removeEventListener('abort', cancel); if (!signal.aborted) this.set(element, property, to) })
    if (wait) await done; return true
  }
  cancelAll() { for (const animation of this.animations) animation.cancel(); this.animations.clear() }
  private css(property: string, value: unknown): Keyframe { if (property === 'Opacity') return { opacity: String(value) }; const state: RecordValue = { [property]: value }; return { transform: this.transformText(state) } }
  private transform(element: HTMLElement) { element.style.transform = this.transformText({ VisualOffsetX: element.dataset.webVisualOffsetX, VisualOffsetY: element.dataset.webVisualOffsetY, ScaleX: element.dataset.webScaleX, ScaleY: element.dataset.webScaleY, Rotation: element.dataset.webRotation }) }
  private transformText(values: RecordValue) { return `translate(${values.VisualOffsetX ?? 0}px,${values.VisualOffsetY ?? 0}px) scale(${values.ScaleX ?? 1},${values.ScaleY ?? 1}) rotate(${values.Rotation ?? 0}deg)` }
}

/** Executes the persisted v3 behavior graph in a single Web page. */
export class WebBehaviorRuntime {
  private document: BehaviorDocument | undefined; private readonly resolver = new WebAnimationTargetResolver(); private readonly adapters = new WebAnimatablePropertyAdapterRegistry(); private readonly active = new Map<string, AbortController[]>(); private readonly loops = new Map<string, AbortController>();
  constructor(private readonly warn: (message: string) => void = console.warn) {}
  replace(document: BehaviorDocument | undefined) { this.dispose(); this.document = document }
  dispose() { for (const controllers of this.active.values()) controllers.forEach(controller => controller.abort()); this.active.clear(); for (const controller of this.loops.values()) controller.abort(); this.loops.clear(); this.adapters.cancelAll() }
  publish(event: BehaviorEvent) { for (const set of this.document?.ControlBehaviorSets ?? []) for (const behavior of set.Behaviors ?? []) if (behavior.Enabled !== false) this.dispatch(set, behavior, event) }
  private dispatch(set: ControlSet, behavior: Behavior, event: BehaviorEvent) { if (behavior.Kind === 'Transition') { if (trigger(behavior.TransitionTrigger, event)) this.warn(`TransitionDeferred:${behavior.BehaviorId}`); return } if (behavior.Kind === 'Loop') { if (trigger(behavior.StartTrigger, event)) this.startLoop(set, behavior, event); if ((behavior.StopTriggers ?? []).some(item => trigger(item, event))) this.stopLoop(set, behavior, event); return } if (trigger(behavior.Trigger, event)) this.start(set, behavior, behavior.Graph, { event, guid: set.BehaviorGuid, display: set.DisplayName }) }
  private start(set: ControlSet, behavior: Behavior, graph: Graph | undefined, context: Context) { const running = this.active.get(behavior.BehaviorId) ?? []; const policy = behavior.ReentryPolicy ?? 'InterruptPrevious'; if (policy === 'IgnoreIfRunning' && running.length) return; if (policy === 'InterruptPrevious') running.forEach(item => item.abort()); const controller = new AbortController(); this.active.set(behavior.BehaviorId, policy === 'AllowParallel' ? [...running, controller] : [controller]); const run = () => this.execute(graph, context, controller.signal).catch(error => !controller.signal.aborted && this.warn(String(error))).finally(() => { const values = this.active.get(behavior.BehaviorId) ?? []; this.active.set(behavior.BehaviorId, values.filter(item => item !== controller)) }); if (policy === 'Queue' && running.length) Promise.all(running.map(item => new Promise<void>(resolve => item.signal.addEventListener('abort', () => resolve(), { once: true })))).then(run); else run() }
  private startLoop(set: ControlSet, behavior: Behavior, event: BehaviorEvent) { if (this.loops.has(behavior.BehaviorId)) return; const controller = new AbortController(); this.loops.set(behavior.BehaviorId, controller); const context = { event, start: event, guid: set.BehaviorGuid, display: set.DisplayName }; void (async () => { await this.execute(behavior.StartGraph, context, controller.signal); const count = number(behavior.LoopPolicy?.RepeatCount) ?? -1; for (let index = 0; !controller.signal.aborted && (count < 0 || index < count); index++) { await this.execute(behavior.LoopGraph, context, controller.signal); const interval = Math.max(0, number(behavior.LoopPolicy?.IntervalMs) ?? 0); if (interval) await this.delay(interval, controller.signal) } })().catch(() => undefined).finally(() => this.loops.delete(behavior.BehaviorId)) }
  private stopLoop(set: ControlSet, behavior: Behavior, event: BehaviorEvent) { const controller = this.loops.get(behavior.BehaviorId); if (!controller) return; controller.abort(); if ((behavior.LoopPolicy?.StopMode ?? 'RunStopGraph') === 'RunStopGraph') { const stop = new AbortController(); void this.execute(behavior.StopGraph, { event, start: undefined, stop: event, guid: set.BehaviorGuid, display: set.DisplayName }, stop.signal) } }
  private async execute(graph: Graph | undefined, context: Context, signal: AbortSignal) { const nodes = new Map((graph?.Nodes ?? []).map(node => [node.NodeId, node])); const edges = graph?.Connections ?? []; const start = [...nodes.values()].find(node => node.NodeType === 'flow.start'); if (!start) return; let steps = 0; const flow = async (node: Node): Promise<void> => { if (signal.aborted || ++steps > 1000) return; const out = async (port: string) => { const edge = edges.find(item => item.SourceNodeId === node.NodeId && item.SourcePort === port); const next = edge && nodes.get(edge.TargetNodeId); if (next) await flow(next) }; const p = node.Properties ?? {}; switch (node.NodeType) { case 'flow.start': await out('Out'); break; case 'flow.end': break; case 'flow.delay': await this.delay(Math.max(0, number(p.DurationMs) ?? 0), signal); await out('Out'); break; case 'flow.parallel': await Promise.all(Array.from({ length: Math.min(20, Math.max(1, number(p.BranchCount) ?? 3)) }, (_, i) => out(`Branch${i + 1}`))); await out('Out'); break; case 'flow.if': await out(compare(resolve(p.Left, context), String(p.Operator ?? 'Equals'), resolve(p.Right, context)) ? 'True' : 'False'); break; case 'action.log': console.info('[WebBehavior]', p.Message ?? ''); await out('Out'); break; case 'action.setProperty': await this.action(node, nodes, edges, context, signal, 'set'); await out('Out'); break; case 'action.resetProperty': await this.action(node, nodes, edges, context, signal, 'reset'); await out('Out'); break; case 'action.animateProperty': await this.action(node, nodes, edges, context, signal, 'animate'); await out('Out'); break; default: if (!node.NodeType.startsWith('value.') && !node.NodeType.startsWith('math.')) this.warn(`UnknownNode:${node.NodeType}`) } }; await flow(start) }
  private async action(node: Node, nodes: Map<string, Node>, edges: Edge[], context: Context, signal: AbortSignal, kind: 'set' | 'reset' | 'animate') { const p = node.Properties ?? {}; const element = this.resolver.resolve(p.Target, context.guid, p.TargetLayer); const property = String(p.PropertyName ?? ''); if (!element) return this.warn(`TargetUnavailable:${String(p.Target)}`); if (kind === 'reset') { this.adapters.reset(element, property); return } const value = this.value(node, nodes, edges, kind === 'animate' ? 'ToInput' : 'ValueInput', kind === 'animate' ? p.To : p.Value, context, new Set()); if (value === undefined || !supported.has(property)) return this.warn(`UnsupportedWebProperty:${property}`); if (kind === 'set') this.adapters.set(element, property, value); else await this.adapters.animate(element, property, this.value(node, nodes, edges, 'FromInput', p.From, context, new Set()) ?? 0, value, p.DurationMs, p.WaitForCompletion !== false, signal) }
  private value(node: Node, nodes: Map<string, Node>, edges: Edge[], port: string, literal: unknown, context: Context, visiting: Set<string>): unknown { const edge = edges.find(item => item.TargetNodeId === node.NodeId && item.TargetPort === port); if (!edge) return this.expression(literal, context); const source = nodes.get(edge.SourceNodeId); if (!source || visiting.has(source.NodeId)) return undefined; visiting.add(source.NodeId); const p = source.Properties ?? {}; const input = (name: string, fallback = 0) => number(this.value(source, nodes, edges, name, fallback, context, visiting)) ?? fallback; let result: number | undefined; switch (source.NodeType) { case 'value.number': result = number(p.Value); break; case 'value.eventContext': result = number(resolve(p.Path, context)) ?? number(p.FallbackValue); break; case 'math.add': result = input('Left') + input('Right'); break; case 'math.subtract': result = input('Left') - input('Right'); break; case 'math.multiply': result = input('Left') * input('Right'); break; case 'math.divide': { const b = input('Right'); result = b === 0 ? undefined : input('Left') / b; break } case 'math.modulo': { const b = input('Right'); result = b === 0 ? undefined : input('Left') % b; break } case 'math.negate': result = -input('Value'); break; case 'math.abs': result = Math.abs(input('Value')); break; case 'math.min': result = Math.min(input('Left'), input('Right')); break; case 'math.max': result = Math.max(input('Left'), input('Right')); break; case 'math.clamp': result = Math.min(input('Max'), Math.max(input('Min'), input('Value'))); break; case 'math.round': result = Math.round(input('Value')); break; case 'math.floor': result = Math.floor(input('Value')); break; case 'math.ceil': result = Math.ceil(input('Value')); break; default: result = undefined } visiting.delete(source.NodeId); return number(result) }
  private expression(value: unknown, context: Context): unknown { if (typeof value !== 'string' || !value.startsWith('=')) return value; const expression = value.slice(1).replace(/(Event|StartEvent|StopEvent)\.([A-Za-z0-9_]+)/g, (_, prefix, name) => String(number(resolve(`${prefix}.${name}`, context)) ?? 'NaN')); if (!/^[0-9+\-*/%().,\sA-Za-z]+$/.test(expression)) return undefined; try { const translated = expression.replace(/\b(clamp|min|max|abs|round|floor|ceil)\b/g, 'Math.$1').replace(/\bMath\.clamp\(/g, 'clamp('); const clamp = (v: number, min: number, max: number) => Math.min(max, Math.max(min, v)); const result = Function('Math', 'clamp', `return (${translated})`)(Math, clamp); return number(result) } catch { return undefined } }
  private delay(milliseconds: number, signal: AbortSignal) { return new Promise<void>((resolve, reject) => { const id = window.setTimeout(resolve, milliseconds); signal.addEventListener('abort', () => { clearTimeout(id); reject(new DOMException('Cancelled', 'AbortError')) }, { once: true }) }) }
}
