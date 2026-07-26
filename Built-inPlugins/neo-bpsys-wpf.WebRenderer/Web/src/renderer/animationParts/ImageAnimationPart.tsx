import type { AnimationPartConfig } from './animationPartTypes'
import { animationPartStyle } from './AnimationPartRenderer'

export function ImageAnimationPart({ config, resources }: { config: AnimationPartConfig; resources: Record<string, string> }) {
  if (!config.Name) return null
  return <img data-animation-part={config.Name} data-runtime-name={config.Name} src={resources[config.ImagePath ?? '']} style={{ ...animationPartStyle(config), objectFit: 'fill' }} draggable={false} />
}
