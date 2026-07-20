import { applyComposedState, composeClipPath, defaultAnimationState, type ElementAnimationState } from './WebAnimationState'
import { formatWebLength, parseWebLength, resolveWebLength, type WebLength } from './WebLength'

export const supportedWebProperties = new Set(['Opacity', 'Visibility', 'VisualOffsetX', 'VisualOffsetY', 'ClipInsetLeft', 'ClipInsetTop', 'ClipInsetRight', 'ClipInsetBottom', 'ScaleX', 'ScaleY', 'Rotation', 'Width', 'Height', 'FillColor', 'StrokeColor', 'StrokeThickness', 'TextColor', 'Foreground', 'FontSize', 'TintColor', 'TintStrength', 'TextureStrength', 'GaussianBlurRadius'])

const numeric = (value: unknown, fallback = 0): number => {
  const parsed = typeof value === 'number' ? value : Number(value)
  return Number.isFinite(parsed) ? parsed : fallback
}

type Baseline = Map<string, unknown>

export class WebAnimatablePropertyAdapterRegistry {
  private readonly states = new Map<HTMLElement, ElementAnimationState>()
  private readonly bases = new Map<HTMLElement, Baseline>()
  private readonly animations = new Map<HTMLElement, Map<string, Animation>>()

  set(element: HTMLElement, property: string, value: unknown): boolean {
    if (!supportedWebProperties.has(property)) return false
    this.capture(element, property)
    this.cancelProperty(element, property)
    this.setInternal(element, property, value)
    return true
  }

  reset(element: HTMLElement, property: string): void {
    const baseline = this.bases.get(element)
    if (!baseline) return
    const properties = property.toLowerCase() === 'all' ? [...baseline.keys()] : [property]
    for (const item of properties) {
      if (!baseline.has(item)) continue
      this.cancelProperty(element, item)
      this.setInternal(element, item, baseline.get(item))
      baseline.delete(item)
    }
    if (baseline.size === 0) this.bases.delete(element)
  }

  async animate(element: HTMLElement, property: string, from: unknown, to: unknown, duration: unknown, wait: boolean, signal: AbortSignal, easing = 'Linear'): Promise<boolean> {
    if (!supportedWebProperties.has(property)) return false
    this.capture(element, property)
    this.cancelProperty(element, property)
    this.setInternal(element, property, from)
    const milliseconds = Math.max(0, numeric(duration))
    if (property === 'Visibility' || milliseconds === 0 || typeof element.animate !== 'function') {
      this.setInternal(element, property, to)
      return true
    }
    const keyframes = this.keyframes(element, property, from, to)
    if (!keyframes) return false
    const animation = element.animate(keyframes, { duration: milliseconds, fill: 'forwards', easing: this.easing(easing) })
    let map = this.animations.get(element)
    if (!map) { map = new Map(); this.animations.set(element, map) }
    map.set(property, animation)
    const cancel = () => animation.cancel()
    signal.addEventListener('abort', cancel, { once: true })
    const done = animation.finished.catch(() => undefined).then(() => {
      signal.removeEventListener('abort', cancel)
      if (map?.get(property) === animation) map.delete(property)
      if (!signal.aborted) {
        this.setInternal(element, property, to)
        animation.cancel()
      }
    })
    if (wait) await done
    return true
  }

  cancelAll(): void {
    for (const map of this.animations.values()) for (const animation of map.values()) animation.cancel()
    this.animations.clear()
    this.bases.clear()
    this.states.clear()
  }

  getState(element: HTMLElement): ElementAnimationState {
    return { ...this.state(element) }
  }

  private state(element: HTMLElement): ElementAnimationState {
    let state = this.states.get(element)
    if (!state) { state = defaultAnimationState(); this.states.set(element, state); applyComposedState(element, state) }
    return state
  }

  private capture(element: HTMLElement, property: string): void {
    let baseline = this.bases.get(element)
    if (!baseline) { baseline = new Map(); this.bases.set(element, baseline) }
    if (!baseline.has(property)) baseline.set(property, this.read(element, property))
  }

  private read(element: HTMLElement, property: string): unknown {
    const state = this.state(element)
    switch (property) {
      case 'VisualOffsetX': return state.visualOffsetX
      case 'VisualOffsetY': return state.visualOffsetY
      case 'ScaleX': return state.scaleX
      case 'ScaleY': return state.scaleY
      case 'Rotation': return state.rotation
      case 'ClipInsetLeft': return state.clipInsetLeft
      case 'ClipInsetTop': return state.clipInsetTop
      case 'ClipInsetRight': return state.clipInsetRight
      case 'ClipInsetBottom': return state.clipInsetBottom
      case 'GaussianBlurRadius': return state.gaussianBlurRadius
      case 'TintColor': return state.tintColor
      case 'TintStrength': return state.tintStrength
      case 'TextureStrength': return state.textureStrength
      case 'Opacity': return element.style.opacity || getComputedStyle(element).opacity
      case 'Visibility': return element.style.display === 'none' ? 'Collapsed' : element.style.visibility === 'hidden' ? 'Hidden' : 'Visible'
      case 'Width': return element.style.width || 'auto'
      case 'Height': return element.style.height || 'auto'
      case 'FillColor': return element.style.backgroundColor
      case 'StrokeColor': return element.style.borderColor
      case 'StrokeThickness': return element.style.borderWidth
      case 'TextColor': case 'Foreground': return element.style.color
      case 'FontSize': return element.style.fontSize
      default: return undefined
    }
  }

  private setInternal(element: HTMLElement, property: string, value: unknown): void {
    const raw = String(value ?? '')
    const state = this.state(element)
    switch (property) {
      case 'Opacity': element.style.opacity = raw; return
      case 'Visibility':
        element.style.visibility = raw.toLowerCase() === 'hidden' ? 'hidden' : 'visible'
        element.style.display = raw.toLowerCase() === 'collapsed' ? 'none' : ''
        return
      case 'Width': case 'Height': {
        const length = typeof value === 'object' && value && 'kind' in value ? value as WebLength : parseWebLength(value)
        if (length) element.style[property.toLowerCase() as 'width' | 'height'] = formatWebLength(length)
        return
      }
      case 'FillColor': element.style.backgroundColor = raw; return
      case 'StrokeColor': element.style.borderColor = raw; return
      case 'StrokeThickness': element.style.borderWidth = formatWebLength(parseWebLength(value) ?? { kind: 'px', value: 0 }); return
      case 'TextColor': case 'Foreground': element.style.color = raw; return
      case 'FontSize': element.style.fontSize = formatWebLength(parseWebLength(value) ?? { kind: 'px', value: numeric(value) }); return
      case 'VisualOffsetX': state.visualOffsetX = this.relativePixels(element, value, true); break
      case 'VisualOffsetY': state.visualOffsetY = this.relativePixels(element, value, false); break
      case 'ScaleX': state.scaleX = numeric(value, state.scaleX); break
      case 'ScaleY': state.scaleY = numeric(value, state.scaleY); break
      case 'Rotation': state.rotation = numeric(value, state.rotation); break
      case 'ClipInsetLeft': state.clipInsetLeft = this.lengthValue(value, state.clipInsetLeft); break
      case 'ClipInsetTop': state.clipInsetTop = this.lengthValue(value, state.clipInsetTop); break
      case 'ClipInsetRight': state.clipInsetRight = this.lengthValue(value, state.clipInsetRight); break
      case 'ClipInsetBottom': state.clipInsetBottom = this.lengthValue(value, state.clipInsetBottom); break
      case 'GaussianBlurRadius': state.gaussianBlurRadius = Math.max(0, numeric(value)); break
      case 'TintColor': state.tintColor = raw || 'transparent'; break
      case 'TintStrength': state.tintStrength = numeric(value, state.tintStrength); break
      case 'TextureStrength': state.textureStrength = numeric(value, state.textureStrength); break
    }
    applyComposedState(element, state)
  }

  private lengthValue(value: unknown, fallback: WebLength): WebLength {
    if (typeof value === 'object' && value && 'kind' in value) return value as WebLength
    return parseWebLength(value) ?? fallback
  }

  private relativePixels(element: HTMLElement, value: unknown, horizontal: boolean): number {
    const length = parseWebLength(value)
    if (!length) return numeric(value)
    const parent = element.closest<HTMLElement>('[data-control-root]') ?? element
    const reference = horizontal ? parent.getBoundingClientRect().width : parent.getBoundingClientRect().height
    return resolveWebLength(length, reference) ?? 0
  }

  private keyframes(element: HTMLElement, property: string, from: unknown, to: unknown): Keyframe[] | null {
    const state = this.state(element)
    if (property === 'Opacity') return [{ opacity: String(from) }, { opacity: String(to) }]
    if (property.startsWith('ClipInset')) {
      const key = `${property[9].toLowerCase()}${property.slice(10)}` as 'left' | 'top' | 'right' | 'bottom'
      const before = { ...state, [`clipInset${key[0].toUpperCase()}${key.slice(1)}`]: this.lengthValue(from, { kind: 'px', value: 0 }) } as ElementAnimationState
      const after = { ...state, [`clipInset${key[0].toUpperCase()}${key.slice(1)}`]: this.lengthValue(to, { kind: 'px', value: 0 }) } as ElementAnimationState
      return [{ clipPath: composeClipPath(before) }, { clipPath: composeClipPath(after) }]
    }
    if (['VisualOffsetX', 'VisualOffsetY', 'ScaleX', 'ScaleY', 'Rotation'].includes(property)) {
      const before = { ...state }; const after = { ...state }
      const stateKey = property[0].toLowerCase() + property.slice(1) as keyof ElementAnimationState
      ;(before[stateKey] as number) = property.startsWith('VisualOffset') ? this.relativePixels(element, from, property.endsWith('X')) : numeric(from)
      ;(after[stateKey] as number) = property.startsWith('VisualOffset') ? this.relativePixels(element, to, property.endsWith('X')) : numeric(to)
      if (property.startsWith('VisualOffset')) return [{ translate: `${before.visualOffsetX}px ${before.visualOffsetY}px` }, { translate: `${after.visualOffsetX}px ${after.visualOffsetY}px` }]
      if (property.startsWith('Scale')) return [{ scale: `${before.scaleX} ${before.scaleY}` }, { scale: `${after.scaleX} ${after.scaleY}` }]
      return [{ rotate: `${before.rotation}deg` }, { rotate: `${after.rotation}deg` }]
    }
    if (property === 'GaussianBlurRadius') return [{ '--web-gaussian-blur-radius': `${numeric(from)}px` }, { '--web-gaussian-blur-radius': `${numeric(to)}px` }] as Keyframe[]
    if (property === 'TintColor') return [{ '--web-tint-color': String(from) }, { '--web-tint-color': String(to) }] as Keyframe[]
    if (property === 'TintStrength' || property === 'TextureStrength') {
      const css = property === 'TintStrength' ? '--web-tint-strength' : '--web-texture-strength'
      return [{ [css]: String(from) }, { [css]: String(to) }] as Keyframe[]
    }
    const css = property === 'FillColor' ? 'backgroundColor' : property === 'StrokeColor' ? 'borderColor' : property === 'StrokeThickness' ? 'borderWidth' : property === 'TextColor' || property === 'Foreground' ? 'color' : property.toLowerCase()
    const a = ['Width', 'Height', 'StrokeThickness', 'FontSize'].includes(property) ? formatWebLength(parseWebLength(from) ?? { kind: 'px', value: numeric(from) }) : String(from)
    const b = ['Width', 'Height', 'StrokeThickness', 'FontSize'].includes(property) ? formatWebLength(parseWebLength(to) ?? { kind: 'px', value: numeric(to) }) : String(to)
    return [{ [css]: a }, { [css]: b }]
  }

  private cancelProperty(element: HTMLElement, property: string): void {
    const map = this.animations.get(element)
    map?.get(property)?.cancel()
    map?.delete(property)
  }

  private easing(name: string): string {
    return ({ Linear: 'linear', SineInOut: 'cubic-bezier(.445,.05,.55,.95)', CubicOut: 'cubic-bezier(.215,.61,.355,1)', CubicIn: 'cubic-bezier(.55,.055,.675,.19)', CubicInOut: 'cubic-bezier(.645,.045,.355,1)', BackOut: 'cubic-bezier(.175,.885,.32,1.275)' } as Record<string, string>)[name] ?? 'linear'
  }
}
