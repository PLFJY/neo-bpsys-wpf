const missingMasks = new Set<string>()
const resolvedMasks = new Set<string>()

export type PickingBorderProps = {
  behaviorGuid: string
  runtimeName?: string
  imageUrl?: string
  fillColor?: string
  zIndex: number
  available?: boolean
}

/** Render the shared alpha-mask target used by Image, BorderedImage and MapV2. */
export function PickingBorderRenderer({ behaviorGuid, runtimeName, imageUrl, fillColor, zIndex, available = true }: PickingBorderProps) {
  if (!available) return null
  if (!imageUrl && runtimeName && !missingMasks.has(runtimeName)) {
    missingMasks.add(runtimeName)
    console.warn(`[Web Renderer] picking border mask missing Target=${runtimeName} Diagnostic=PickingBorderResourceMissing`)
  } else if (imageUrl && runtimeName && !resolvedMasks.has(runtimeName)) {
    resolvedMasks.add(runtimeName)
    console.debug(`[Web Renderer] picking border mask resolved Target=${runtimeName}`)
  }
  return <div data-animation-part="PickingBorder" data-runtime-name={runtimeName ?? ''} data-picking-border data-behavior-guid={behaviorGuid} style={{ position: 'absolute', inset: 0, zIndex, backgroundColor: imageUrl ? (fillColor || '#fff') : 'transparent', maskImage: imageUrl ? `url(${imageUrl})` : undefined, WebkitMaskImage: imageUrl ? `url(${imageUrl})` : undefined, maskRepeat: 'no-repeat', WebkitMaskRepeat: 'no-repeat', maskPosition: 'center', WebkitMaskPosition: 'center', maskSize: '100% 100%', WebkitMaskSize: '100% 100%', maskMode: 'alpha', WebkitMaskComposite: 'source-over', opacity: 0, visibility: 'hidden', pointerEvents: 'none' }} />
}
