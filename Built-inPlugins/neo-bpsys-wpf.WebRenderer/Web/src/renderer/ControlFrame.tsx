import type { CSSProperties, ReactNode } from 'react'
import { finite, type BaseConfig } from './controlTypes'
import { visibilityStyle } from './visibility'

export function ControlFrame({ name, config, children }: { name: string; config: BaseConfig; children: ReactNode }) {
  const layout: CSSProperties = { position: 'absolute', left: finite(config.Left), top: finite(config.Top), zIndex: finite(config.ZIndex), width: typeof config.Width === 'number' ? config.Width : undefined, height: typeof config.Height === 'number' ? config.Height : undefined, pointerEvents: 'none', ...visibilityStyle(config.Visibility) }
  const blur = config.IsGaussianBlurEnabled && finite(config.GaussianBlurRadius) > 0 ? `blur(${finite(config.GaussianBlurRadius)}px)` : undefined
  return <div data-control-name={name} data-behavior-guid={config.BehaviorGuid} style={layout}>
    <div data-effect-host style={{ filter: blur }}>
      <div data-control-root data-control><div data-overlay-below />{children}<div data-overlay-above /></div>
    </div>
  </div>
}
