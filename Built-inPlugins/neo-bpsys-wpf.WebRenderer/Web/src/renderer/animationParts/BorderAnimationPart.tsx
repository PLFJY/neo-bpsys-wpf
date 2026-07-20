import type { AnimationPartConfig } from './animationPartTypes'
import { AnimationPartRenderer } from './AnimationPartRenderer'

export function BorderAnimationPart({ config }: { config: AnimationPartConfig }) {
  return <AnimationPartRenderer config={config} />
}
