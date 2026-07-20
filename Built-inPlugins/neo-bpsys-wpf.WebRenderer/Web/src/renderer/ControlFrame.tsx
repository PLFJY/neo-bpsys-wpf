import type { CSSProperties, ReactNode } from 'react'
import { finite, type BaseConfig } from './controlTypes'
import { visibilityStyle } from './visibility'
import type { ControlBehaviorSet } from '../behavior/behaviorTypes'
import { AnimationPartRegistry } from './animationParts/AnimationPartRegistry'

export function ControlFrame({ name, config, children, semanticChild = false, behaviorSet, resources = {} }: { name: string; config: BaseConfig; children: ReactNode; semanticChild?: boolean; behaviorSet?: ControlBehaviorSet; resources?: Record<string, string> }) {
  const layout: CSSProperties = { position: 'absolute', left: finite(config.Left), top: finite(config.Top), zIndex: finite(config.ZIndex), pointerEvents: 'none' }
  const staticBlur = config.IsGaussianBlurEnabled && finite(config.GaussianBlurRadius) > 0 ? finite(config.GaussianBlurRadius) : 0
  const effectStyle = { '--web-static-blur-radius': `${staticBlur}px`, filter: 'blur(calc(var(--web-static-blur-radius, 0px) + var(--web-gaussian-blur-radius, 0px)))' } as CSSProperties
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
