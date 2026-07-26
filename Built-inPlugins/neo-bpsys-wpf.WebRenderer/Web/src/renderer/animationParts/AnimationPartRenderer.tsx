import type { CSSProperties } from 'react'
import type { AnimationPartConfig } from './animationPartTypes'

const length = (text: string | null | undefined, number: number | null | undefined): CSSProperties['width'] => {
  if (text?.trim()) {
    const value = text.trim()
    if (value.toLowerCase() === 'auto') return 'auto'
    if (/^[+-]?(?:\d+(?:\.\d+)?|\.\d+)%$/.test(value)) return value
    if (/^[+-]?(?:\d+(?:\.\d+)?|\.\d+)(?:px)?$/i.test(value)) return Number.parseFloat(value)
  }
  return typeof number === 'number' && Number.isFinite(number) ? Math.max(0, number) : undefined
}

const color = (value: string | null | undefined, opacity = 1): string | undefined => {
  if (!value) return undefined
  const match = /^#([0-9a-f]{8})$/i.exec(value)
  if (!match) return value
  const alpha = Number.parseInt(match[1].slice(0, 2), 16) / 255 * Math.max(0, Math.min(1, opacity))
  const red = Number.parseInt(match[1].slice(2, 4), 16)
  const green = Number.parseInt(match[1].slice(4, 6), 16)
  const blue = Number.parseInt(match[1].slice(6, 8), 16)
  return `rgba(${red}, ${green}, ${blue}, ${alpha})`
}

const effect = (config: AnimationPartConfig): string | undefined => {
  const value = config.Effect
  if (!value || value.Kind === 'None') return undefined
  const depth = value.Kind === 'Glow' ? 0 : Math.max(0, value.ShadowDepth ?? 0)
  const radians = (value.Direction ?? 0) * Math.PI / 180
  const x = Math.cos(radians) * depth; const y = -Math.sin(radians) * depth
  return `drop-shadow(${x}px ${y}px ${Math.max(0, value.BlurRadius ?? 0)}px ${color(value.Color, value.Opacity) ?? '#000000'})`
}

export function animationPartStyle(config: AnimationPartConfig): CSSProperties {
  return { position: 'absolute', left: config.Left ?? 0, top: config.Top ?? 0, width: length(config.WidthText, config.Width), height: length(config.HeightText, config.Height), background: color(config.Fill), borderColor: color(config.Stroke), borderStyle: config.Stroke ? 'solid' : undefined, borderWidth: Math.max(0, config.StrokeThickness ?? 0), opacity: config.Opacity ?? 1, visibility: String(config.Visibility ?? 'Hidden').toLowerCase() === 'visible' ? 'visible' : 'hidden', display: String(config.Visibility).toLowerCase() === 'collapsed' ? 'none' : undefined, zIndex: config.ZIndex ?? 0, pointerEvents: config.IsHitTestVisible ? 'auto' : 'none', filter: effect(config), boxSizing: 'border-box' }
}

export function AnimationPartRenderer({ config }: { config: AnimationPartConfig }) {
  if (!config.Name) return null
  return <div data-animation-part={config.Name} data-runtime-name={config.Name} data-animation-part-kind={config.Kind ?? 'Rectangle'} style={animationPartStyle(config)} />
}
