import type { RuntimeState, WebRuntimeAsset } from '../../protocol/runtime'
import type { ControlBehaviorSet } from '../../behavior/behaviorTypes'
import type { ImageConfig } from '../controlTypes'
import { visibilityStyle } from '../visibility'
import { AnimationPartRegistry } from '../animationParts/AnimationPartRegistry'
import { ImageOverlays } from './ImageOverlays'
import { ImageViewport } from './ImageViewport'

export function ImageRenderer({ name, config, runtime, resources, behaviorSet }: { name: string; config: ImageConfig; runtime: RuntimeState; resources: Record<string, string>; behaviorSet?: ControlBehaviorSet }) {
  const value = config.BindingPath ? runtime.values[config.BindingPath] : undefined
  const asset = value && typeof value === 'object' && (value as WebRuntimeAsset).Kind === 'image' ? value as WebRuntimeAsset : undefined
  const source = config.BindingPath ? asset?.Url ?? null : resources[config.ImagePath ?? ''] ?? null
  const rootStyle = { position: 'relative' as const, width: typeof config.Width === 'number' ? config.Width : undefined, height: typeof config.Height === 'number' ? config.Height : undefined, overflow: config.ClipToBounds || (config.CornerRadius ?? 0) > 0 ? 'hidden' : 'visible', borderRadius: config.CornerRadius || undefined, ...visibilityStyle(config.Visibility) }
  return <div className="web-image" data-image-semantic-root data-control-root data-control-name={name} data-runtime-name={name} data-behavior-guid={config.BehaviorGuid} style={rootStyle}>
    <div data-overlay-below><AnimationPartRegistry parts={behaviorSet?.AnimationParts} layer="BelowContent" resources={resources} /></div>
    <ImageViewport config={config} source={source} asset={asset} generation={runtime.generation} />
    <div data-overlay-above><ImageOverlays name={name} config={config} runtime={runtime} resources={resources} /><AnimationPartRegistry parts={behaviorSet?.AnimationParts} layer="AboveContent" resources={resources} /></div>
  </div>
}
