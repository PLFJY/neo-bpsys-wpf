export type WebRenderContext = {
  canvasWidth: number
  canvasHeight: number
  backgroundUrl?: string
  backgroundRevision?: string
  resources: Record<string, string>
  defaultPickingBorderResourceUrl?: string
}
