import type { AnimationPartConfig } from './animationPartTypes'
import { BorderAnimationPart } from './BorderAnimationPart'
import { ImageAnimationPart } from './ImageAnimationPart'
import { RectangleAnimationPart } from './RectangleAnimationPart'

function renderPart(part: AnimationPartConfig, resources: Record<string, string>) {
  switch (part.Kind ?? 'Rectangle') {
    case 'Border': return <BorderAnimationPart key={part.Name} config={part} />
    case 'Image': return <ImageAnimationPart key={part.Name} config={part} resources={resources} />
    case 'Rectangle': return <RectangleAnimationPart key={part.Name} config={part} />
    default: return null
  }
}

export function AnimationPartRegistry({ parts, layer, resources }: { parts?: AnimationPartConfig[]; layer: 'BelowContent' | 'AboveContent'; resources: Record<string, string> }) {
  return <>{(parts ?? []).filter(part => (part.Layer ?? 'AboveContent') === layer).map(part => renderPart(part, resources))}</>
}
