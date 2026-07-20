import { WebAnimatablePropertyAdapterRegistry, supportedWebProperties } from './WebAnimatablePropertyAdapters'
import { WebAnimationTargetResolver } from './WebAnimationTargetResolver'
import { compare, numberValue } from './WebBehaviorComparators'
import type { BehaviorContext, BehaviorDocument, BehaviorEdge, BehaviorEvent, BehaviorGraph, BehaviorNode, BehaviorTrigger, ControlBehaviorSet, FrontedBehavior, RecordValue } from './behaviorTypes'

const payloadKey = (path: string, payload: RecordValue): unknown => Object.prototype.hasOwnProperty.call(payload, path) ? payload[path] : payload[`Event.${path}`]

const resolve = (path: unknown, context: BehaviorContext): unknown => {
  if (typeof path !== 'string') return path
  const find = (prefix: string, value: BehaviorEvent | undefined) => path.startsWith(prefix) ? payloadKey(path.slice(prefix.length), value?.Payload ?? {}) : undefined
  const event = find('Event.', context.event); if (event !== undefined) return event
  const start = find('StartEvent.', context.start); if (start !== undefined) return start
  const stop = find('StopEvent.', context.stop); if (stop !== undefined) return stop
  if (path === 'Context.TriggerEventType') return context.event.EventType
  if (path === 'Context.CurrentControlDisplayName') return context.display
  if (path === 'Context.BehaviorGuid') return context.guid
  return path
}

const trigger = (descriptor: BehaviorTrigger | undefined, event: BehaviorEvent): boolean => !!descriptor && descriptor.EventType === event.EventType && (descriptor.Filters ?? []).every(filter => compare(resolve(filter.Left, { event, guid: '' }), filter.Operator, filter.Right))

type TransitionEntry = { set: ControlBehaviorSet; behavior: FrontedBehavior; context: BehaviorContext }

export class WebBehaviorRuntime {
  private document: BehaviorDocument | undefined
  private readonly resolver = new WebAnimationTargetResolver()
  private readonly adapters = new WebAnimatablePropertyAdapterRegistry()
  private readonly active = new Map<string, AbortController[]>()
  private readonly loops = new Map<string, AbortController>()
  private readonly transitions = new Map<string, { controller: AbortController; entries: TransitionEntry[] }>()

  constructor(private readonly warn: (message: string) => void = console.warn) {}

  replace(document: BehaviorDocument | undefined): void { this.dispose(); this.document = document }

  dispose(): void {
    for (const controllers of this.active.values()) controllers.forEach(controller => controller.abort())
    this.active.clear()
    for (const controller of this.loops.values()) controller.abort()
    this.loops.clear()
    for (const transition of this.transitions.values()) transition.controller.abort()
    this.transitions.clear()
    this.adapters.cancelAll()
  }

  async prepareTransition(payload: { correlationId?: string; requests?: RecordValue[] }): Promise<boolean> {
    const correlationId = payload.correlationId
    if (!correlationId || this.transitions.has(correlationId)) return false
    const controller = new AbortController()
    const entries: TransitionEntry[] = []
    for (const request of payload.requests ?? []) for (const set of this.document?.ControlBehaviorSets ?? []) {
      if (String(request.TargetBehaviorGuid ?? request.targetBehaviorGuid ?? '').toLowerCase() !== set.BehaviorGuid.toLowerCase()) continue
      for (const behavior of set.Behaviors ?? []) if (behavior.Enabled !== false && behavior.Kind === 'Transition') {
        const event: BehaviorEvent = { EventType: String(request.TransitionType ?? request.transitionType ?? ''), Payload: (request.Payload ?? request.payload ?? {}) as RecordValue }
        if (trigger(behavior.TransitionTrigger, event)) entries.push({ set, behavior, context: { event, guid: set.BehaviorGuid, display: set.DisplayName } })
      }
    }
    this.transitions.set(correlationId, { controller, entries })
    await Promise.all(entries.map(entry => this.execute(entry.behavior.ExitGraph, entry.context, controller.signal).catch(() => undefined)))
    return !controller.signal.aborted
  }

  async commitTransition(correlationId: string): Promise<boolean> {
    const transition = this.transitions.get(correlationId)
    if (!transition) return false
    console.info(`[Web Renderer] transition enter started. correlationId=${correlationId}`)
    await Promise.all(transition.entries.map(entry => this.execute(entry.behavior.EnterGraph, entry.context, transition.controller.signal).catch(() => undefined)))
    this.transitions.delete(correlationId)
    console.info(`[Web Renderer] transition enter completed. correlationId=${correlationId}`)
    return !transition.controller.signal.aborted
  }

  cancelTransition(correlationId: string): void {
    const transition = this.transitions.get(correlationId)
    if (!transition) return
    transition.controller.abort()
    this.transitions.delete(correlationId)
  }

  publish(event: BehaviorEvent): void {
    console.debug(`[Web Renderer] behavior.event received EventType=${event.EventType} WindowType=${event.WindowType ?? ''}`)
    for (const set of this.document?.ControlBehaviorSets ?? []) for (const behavior of set.Behaviors ?? []) if (behavior.Enabled !== false) this.dispatch(set, behavior, event)
  }

  private dispatch(set: ControlBehaviorSet, behavior: FrontedBehavior, event: BehaviorEvent): void {
    if (behavior.Kind === 'Transition') { if (trigger(behavior.TransitionTrigger, event)) this.warn(`TransitionDeferred:${behavior.BehaviorId}`); return }
    if (behavior.Kind === 'Loop') {
      const starts = trigger(behavior.StartTrigger, event); const stops = (behavior.StopTriggers ?? []).some(item => trigger(item, event))
      if (starts) this.startLoop(set, behavior, event); if (stops) this.stopLoop(set, behavior, event)
      if (starts || stops) console.debug(`[Web Renderer] behavior.trigger matched EventType=${event.EventType} BehaviorId=${behavior.BehaviorId} BehaviorGuid=${set.BehaviorGuid}`)
      return
    }
    const matched = trigger(behavior.Trigger, event)
    console.debug(`[Web Renderer] behavior.trigger ${matched ? 'matched' : 'rejected'} EventType=${event.EventType} BehaviorId=${behavior.BehaviorId} BehaviorGuid=${set.BehaviorGuid}`)
    if (matched) this.start(behavior, behavior.Graph, { event, guid: set.BehaviorGuid, display: set.DisplayName })
  }

  private start(behavior: FrontedBehavior, graph: BehaviorGraph | undefined, context: BehaviorContext): void {
    const running = this.active.get(behavior.BehaviorId) ?? []
    const policy = behavior.ReentryPolicy ?? 'InterruptPrevious'
    if (policy === 'IgnoreIfRunning' && running.length) return
    if (policy === 'InterruptPrevious') running.forEach(item => item.abort())
    const controller = new AbortController()
    this.active.set(behavior.BehaviorId, policy === 'AllowParallel' ? [...running, controller] : [controller])
    const run = () => this.execute(graph, context, controller.signal).catch(error => !controller.signal.aborted && this.warn(String(error))).finally(() => {
      const values = this.active.get(behavior.BehaviorId) ?? []
      this.active.set(behavior.BehaviorId, values.filter(item => item !== controller))
    })
    if (policy === 'Queue' && running.length) Promise.all(running.map(item => new Promise<void>(done => item.signal.addEventListener('abort', () => done(), { once: true })))).then(run)
    else run()
  }

  private startLoop(set: ControlBehaviorSet, behavior: FrontedBehavior, event: BehaviorEvent): void {
    if (this.loops.has(behavior.BehaviorId)) return
    const controller = new AbortController(); this.loops.set(behavior.BehaviorId, controller)
    console.debug(`[Web Renderer] loop started BehaviorId=${behavior.BehaviorId} BehaviorGuid=${set.BehaviorGuid}`)
    const context = { event, start: event, guid: set.BehaviorGuid, display: set.DisplayName }
    void (async () => {
      await this.execute(behavior.StartGraph, context, controller.signal)
      const count = numberValue(behavior.LoopPolicy?.RepeatCount) ?? -1
      for (let index = 0; !controller.signal.aborted && (count < 0 || index < count); index++) {
        await this.execute(behavior.LoopGraph, context, controller.signal)
        const interval = Math.max(0, numberValue(behavior.LoopPolicy?.IntervalMs) ?? 0)
        if (interval) await this.delay(interval, controller.signal)
      }
    })().catch(() => undefined).finally(() => { this.loops.delete(behavior.BehaviorId); console.debug(`[Web Renderer] loop stopped BehaviorId=${behavior.BehaviorId} BehaviorGuid=${set.BehaviorGuid}`) })
  }

  private stopLoop(set: ControlBehaviorSet, behavior: FrontedBehavior, event: BehaviorEvent): void {
    const controller = this.loops.get(behavior.BehaviorId); if (!controller) return
    controller.abort()
    console.debug(`[Web Renderer] loop stop requested EventType=${event.EventType} BehaviorId=${behavior.BehaviorId} BehaviorGuid=${set.BehaviorGuid}`)
    if ((behavior.LoopPolicy?.StopMode ?? 'RunStopGraph') === 'RunStopGraph') void this.execute(behavior.StopGraph, { event, stop: event, guid: set.BehaviorGuid, display: set.DisplayName }, new AbortController().signal)
  }

  private async execute(graph: BehaviorGraph | undefined, context: BehaviorContext, signal: AbortSignal): Promise<void> {
    const nodes = new Map((graph?.Nodes ?? []).map(node => [node.NodeId, node]))
    const edges = graph?.Connections ?? []
    const start = [...nodes.values()].find(node => node.NodeType === 'flow.start')
    if (!start) return
    let steps = 0
    const flow = async (node: BehaviorNode, visited?: Set<string>): Promise<void> => {
      if (signal.aborted || ++steps > 1000) return
      if (visited?.has(node.NodeId)) return
      visited?.add(node.NodeId)
      const nextFor = (port: string): BehaviorNode | undefined => {
        const edge = edges.find(item => item.SourceNodeId === node.NodeId && item.SourcePort === port)
        return edge ? nodes.get(edge.TargetNodeId) : undefined
      }
      const out = async (port: string, branchVisited = visited) => { const next = nextFor(port); if (next) await flow(next, branchVisited) }
      const p = node.Properties ?? {}
      switch (node.NodeType) {
        case 'flow.start': await out('Out'); break
        case 'flow.end': break
        case 'flow.delay': await this.delay(Math.max(0, numberValue(p.DurationMs) ?? 0), signal); await out('Out'); break
        case 'flow.parallel': {
          const count = Math.min(20, Math.max(1, numberValue(p.BranchCount) ?? 3))
          const unique = new Map<string, BehaviorNode>()
          for (let index = 1; index <= count; index++) { const branch = nextFor(`Branch${index}`); if (branch) unique.set(branch.NodeId, branch) }
          const branchVisited = new Set(visited)
          await Promise.all([...unique.values()].map(branch => flow(branch, branchVisited)))
          await out('Out', branchVisited)
          break
        }
        case 'flow.if': await out(compare(resolve(p.Left, context), String(p.Operator ?? 'Equals'), resolve(p.Right, context)) ? 'True' : 'False'); break
        case 'action.log': console.info('[WebBehavior]', p.Message ?? ''); await out('Out'); break
        case 'action.setProperty': await this.action(node, nodes, edges, context, signal, 'set'); await out('Out'); break
        case 'action.resetProperty': await this.action(node, nodes, edges, context, signal, 'reset'); await out('Out'); break
        case 'action.animateProperty': await this.action(node, nodes, edges, context, signal, 'animate'); await out('Out'); break
        default: if (!node.NodeType.startsWith('value.') && !node.NodeType.startsWith('math.')) this.warn(`UnknownNode:${node.NodeType}`)
      }
    }
    await flow(start)
  }

  private async action(node: BehaviorNode, nodes: Map<string, BehaviorNode>, edges: BehaviorEdge[], context: BehaviorContext, signal: AbortSignal, kind: 'set' | 'reset' | 'animate'): Promise<void> {
    const p = node.Properties ?? {}
    let element = this.resolver.resolve(p.Target, context.guid, p.TargetLayer)
    const property = String(p.PropertyName ?? '')
    if (!element) { this.warn(`TargetUnavailable:${String(p.Target)} BehaviorGuid=${context.guid}`); return }
    console.debug(`[Web Renderer] behavior target resolved BehaviorId=${node.NodeId} BehaviorGuid=${context.guid} Target=${String(p.Target)}`)
    if (property === 'GaussianBlurRadius') element = element.closest<HTMLElement>('[data-effect-host]') ?? element
    if (kind === 'reset') { this.adapters.reset(element, property); return }
    const value = this.value(node, nodes, edges, kind === 'animate' ? 'ToInput' : 'ValueInput', kind === 'animate' ? p.To : p.Value, context, new Set())
    if (value === undefined || !supportedWebProperties.has(property)) { this.warn(`UnsupportedWebProperty:${property}`); return }
    if (kind === 'set') this.adapters.set(element, property, value)
    else await this.adapters.animate(element, property, this.value(node, nodes, edges, 'FromInput', p.From, context, new Set()) ?? 0, value, p.DurationMs, p.WaitForCompletion !== false, signal, String(p.Easing ?? 'Linear'))
  }

  private value(node: BehaviorNode, nodes: Map<string, BehaviorNode>, edges: BehaviorEdge[], port: string, literal: unknown, context: BehaviorContext, visiting: Set<string>): unknown {
    const edge = edges.find(item => item.TargetNodeId === node.NodeId && item.TargetPort === port)
    if (!edge) return this.expression(literal, context)
    const source = nodes.get(edge.SourceNodeId); if (!source || visiting.has(source.NodeId)) return undefined
    visiting.add(source.NodeId)
    const p = source.Properties ?? {}; const input = (name: string, fallback = 0) => numberValue(this.value(source, nodes, edges, name, fallback, context, visiting)) ?? fallback
    let result: number | undefined
    switch (source.NodeType) {
      case 'value.number': result = numberValue(p.Value); break
      case 'value.eventContext': result = numberValue(resolve(p.Path, context)) ?? numberValue(p.FallbackValue); break
      case 'math.add': result = input('Left') + input('Right'); break
      case 'math.subtract': result = input('Left') - input('Right'); break
      case 'math.multiply': result = input('Left') * input('Right'); break
      case 'math.divide': { const b = input('Right'); result = b === 0 ? undefined : input('Left') / b; break }
      case 'math.modulo': { const b = input('Right'); result = b === 0 ? undefined : input('Left') % b; break }
      case 'math.negate': result = -input('Value'); break
      case 'math.abs': result = Math.abs(input('Value')); break
      case 'math.min': result = Math.min(input('Left'), input('Right')); break
      case 'math.max': result = Math.max(input('Left'), input('Right')); break
      case 'math.clamp': result = Math.min(input('Max'), Math.max(input('Min'), input('Value'))); break
      case 'math.round': result = Math.round(input('Value')); break
      case 'math.floor': result = Math.floor(input('Value')); break
      case 'math.ceil': result = Math.ceil(input('Value')); break
      default: result = undefined
    }
    visiting.delete(source.NodeId)
    return numberValue(result)
  }

  private expression(value: unknown, context: BehaviorContext): unknown {
    if (typeof value !== 'string' || !value.startsWith('=')) return value
    const expression = value.slice(1).replace(/(Event|StartEvent|StopEvent)\.([A-Za-z0-9_]+)/g, (_, prefix, name) => String(numberValue(resolve(`${prefix}.${name}`, context)) ?? 'NaN'))
    if (!/^[0-9+\-*/%().,\sA-Za-z]+$/.test(expression)) return undefined
    try {
      const translated = expression.replace(/\b(clamp|min|max|abs|round|floor|ceil)\b/g, 'Math.$1').replace(/\bMath\.clamp\(/g, 'clamp(')
      const clamp = (v: number, min: number, max: number) => Math.min(max, Math.max(min, v))
      return numberValue(Function('Math', 'clamp', `return (${translated})`)(Math, clamp))
    } catch { return undefined }
  }

  private delay(milliseconds: number, signal: AbortSignal): Promise<void> {
    return new Promise<void>((done, reject) => {
      const id = window.setTimeout(done, milliseconds)
      signal.addEventListener('abort', () => { clearTimeout(id); reject(new DOMException('Cancelled', 'AbortError')) }, { once: true })
    })
  }
}
