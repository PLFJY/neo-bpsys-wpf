import type { AnimationPartConfig } from './animationPartTypes'
import { AnimationPartRenderer } from './AnimationPartRenderer'

export function RectangleAnimationPart({ config }: { config: AnimationPartConfig }) {
  return <AnimationPartRenderer config={config} />
}
