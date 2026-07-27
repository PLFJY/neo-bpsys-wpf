import type { CSSProperties, ReactNode } from 'react'
import { finite, type BaseConfig } from './controlTypes'
import { effectColor } from './colors'
import { visibilityStyle } from './visibility'
import type { ControlBehaviorSet } from '../behavior/behaviorTypes'
import { AnimationPartRegistry } from './animationParts/AnimationPartRegistry'

export function ControlFrame({ name, config, children, semanticChild = false, behaviorSet, resources = {} }: { name: string; config: BaseConfig; children: ReactNode; semanticChild?: boolean; behaviorSet?: ControlBehaviorSet; resources?: Record<string, string> }) {
  const layout: CSSProperties = { position: 'absolute', left: finite(config.Left), top: finite(config.Top), zIndex: finite(config.ZIndex), pointerEvents: 'none' }
  const staticBlur = config.IsGaussianBlurEnabled && finite(config.GaussianBlurRadius) > 0 ? finite(config.GaussianBlurRadius) : 0

  // Build the CSS filter chain mirroring the WPF Border-wrapped effect stack
  // (FrontedEffectHostFactory.BuildEffectChain: Content -> Blur -> Glow -> Shadow, outermost last).
  // CSS filter functions apply left-to-right, each to the previous result, so the order
  // below reproduces the same visual stacking as WPF: blur first (innermost), then glow,
  // then shadow (outermost).
  const filters: string[] = []
  // Blur: keep the CSS variable structure so GaussianBlur animation can still override
  // the radius via --web-gaussian-blur-radius without rebuilding this filter string.
  filters.push('blur(calc(var(--web-static-blur-radius, 0px) + var(--web-gaussian-blur-radius, 0px)))')
  // Glow: DropShadowEffect with ShadowDepth=0 (no offset, surrounds the element).
  if (config.IsGlowEnabled) {
    const glowRadius = Math.max(0, finite(config.GlowRadius, 20))
    const glowOpacity = Math.min(1, Math.max(0, finite(config.GlowOpacity, 1)))
    const glowColor = effectColor(config.GlowColor, glowOpacity, '#FFFFFFFF')
    filters.push(`drop-shadow(0 0 ${glowRadius}px ${glowColor})`)
  }
  // Shadow: DropShadowEffect with direction-based offset. WPF measures Direction in math
  // convention (counter-clockwise from +X, Y-up); CSS Y is down, so negate sin for the
  // vertical component. Default Direction=315 -> lower-right offset, matching WPF.
  if (config.IsShadowEnabled) {
    const shadowRadius = Math.max(0, finite(config.ShadowRadius, 5))
    const shadowDepth = Math.max(0, finite(config.ShadowDepth, 5))
    const shadowDirection = finite(config.ShadowDirection, 315)
    const shadowOpacity = Math.min(1, Math.max(0, finite(config.ShadowOpacity, 1)))
    const shadowColor = effectColor(config.ShadowColor, shadowOpacity, '#FF000000')
    const rad = shadowDirection * Math.PI / 180
    const offsetX = shadowDepth * Math.cos(rad)
    const offsetY = -shadowDepth * Math.sin(rad)
    filters.push(`drop-shadow(${offsetX}px ${offsetY}px ${shadowRadius}px ${shadowColor})`)
  }

  const effectStyle = { '--web-static-blur-radius': `${staticBlur}px`, filter: filters.join(' ') } as CSSProperties
  const rootStyle: CSSProperties = { position: 'relative', width: typeof config.Width === 'number' ? config.Width : undefined, height: typeof config.Height === 'number' ? config.Height : undefined, ...visibilityStyle(config.Visibility) }
  return <div data-layout-carrier style={layout}>
    <div data-effect-host style={effectStyle}>
      {semanticChild ? children : <div data-control-root data-control data-control-name={name} data-runtime-name={name} data-behavior-guid={config.BehaviorGuid} style={rootStyle}>
        <div data-overlay-below><AnimationPartRegistry parts={behaviorSet?.AnimationParts} layer="BelowContent" resources={resources} /></div>
        {children}
        <div data-overlay-above><AnimationPartRegistry parts={behaviorSet?.AnimationParts} layer="AboveContent" resources={resources} /></div>
      </div>}
    </div>
  </div>
}
