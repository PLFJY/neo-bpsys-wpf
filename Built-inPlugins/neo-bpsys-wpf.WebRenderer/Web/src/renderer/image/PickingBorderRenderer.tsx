import { finite } from '../controlTypes'

export function PickingBorderRenderer({ name, behaviorGuid, available = true, imagePath, fillColor, zIndexOffset, resources }: { name: string; behaviorGuid?: string; available?: boolean; imagePath?: string; fillColor?: string; zIndexOffset?: number; resources: Record<string, string> }) {
  if (!available) return null
  const mask = resources[imagePath ?? ''] ?? resources['Resources/pickingBorder.png']
  return <div data-animation-part="PickingBorder" data-runtime-name={`${name}PickingBorder`} data-picking-border data-behavior-guid={behaviorGuid} style={{ position: 'absolute', inset: 0, zIndex: finite(zIndexOffset, 2), backgroundColor: fillColor || '#fff', maskImage: mask ? `url(${mask})` : undefined, WebkitMaskImage: mask ? `url(${mask})` : undefined, maskRepeat: 'no-repeat', WebkitMaskRepeat: 'no-repeat', maskPosition: 'center', WebkitMaskPosition: 'center', maskSize: '100% 100%', WebkitMaskSize: '100% 100%', maskMode: 'alpha', WebkitMaskComposite: 'source-over', opacity: 0, visibility: 'hidden', pointerEvents: 'none' }} />
}
