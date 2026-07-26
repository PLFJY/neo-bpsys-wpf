import { formatWebLength, type WebLength } from './WebLength'

if (typeof CSS !== 'undefined' && 'registerProperty' in CSS) {
  for (const definition of [
    { name: '--web-gaussian-blur-radius', syntax: '<length>', initialValue: '0px' },
    { name: '--web-tint-strength', syntax: '<number>', initialValue: '1' },
    { name: '--web-texture-strength', syntax: '<number>', initialValue: '1' },
  ]) {
    try { CSS.registerProperty({ ...definition, inherits: false }) } catch { /* Already registered by another runtime instance. */ }
  }
}

export type ElementAnimationState = {
  visualOffsetX: number
  visualOffsetY: number
  scaleX: number
  scaleY: number
  rotation: number
  clipInsetLeft: WebLength
  clipInsetTop: WebLength
  clipInsetRight: WebLength
  clipInsetBottom: WebLength
  gaussianBlurRadius: number
  tintColor: string
  tintStrength: number
  textureStrength: number
}

export const defaultAnimationState = (): ElementAnimationState => ({
  visualOffsetX: 0,
  visualOffsetY: 0,
  scaleX: 1,
  scaleY: 1,
  rotation: 0,
  clipInsetLeft: { kind: 'px', value: 0 },
  clipInsetTop: { kind: 'px', value: 0 },
  clipInsetRight: { kind: 'px', value: 0 },
  clipInsetBottom: { kind: 'px', value: 0 },
  gaussianBlurRadius: 0,
  tintColor: 'transparent',
  tintStrength: 1,
  textureStrength: 1,
})

export function composeTransform(state: ElementAnimationState): string {
  return `translate(${state.visualOffsetX}px, ${state.visualOffsetY}px) scale(${state.scaleX}, ${state.scaleY}) rotate(${state.rotation}deg)`
}

export function composeClipPath(state: ElementAnimationState): string {
  return `inset(${formatWebLength(state.clipInsetTop)} ${formatWebLength(state.clipInsetRight)} ${formatWebLength(state.clipInsetBottom)} ${formatWebLength(state.clipInsetLeft)})`
}

export function applyComposedState(element: HTMLElement, state: ElementAnimationState): void {
  element.style.transformOrigin = '50% 50%'
  element.style.translate = `${state.visualOffsetX}px ${state.visualOffsetY}px`
  element.style.scale = `${state.scaleX} ${state.scaleY}`
  element.style.rotate = `${state.rotation}deg`
  element.style.clipPath = composeClipPath(state)
  element.style.setProperty('--web-gaussian-blur-radius', `${state.gaussianBlurRadius}px`)
  element.style.setProperty('--web-tint-color', state.tintColor)
  element.style.setProperty('--web-tint-strength', String(state.tintStrength))
  element.style.setProperty('--web-texture-strength', String(state.textureStrength))
}
