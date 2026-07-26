import type { CSSProperties } from 'react'
import type { ImageLayoutInput, ImageLayoutResult } from './imageTypes'

const positive = (value: unknown, fallback: number): number => typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : fallback

function offset(alignment: unknown, available: number, content: number): number {
  const remaining = available - content
  switch (String(alignment ?? 'Stretch')) {
    case 'Left': case 'Top': return 0
    case 'Right': case 'Bottom': return remaining
    default: return remaining / 2
  }
}

export function calculateImageLayout(input: ImageLayoutInput): ImageLayoutResult {
  const outerWidth = positive(input.outerWidth, 1)
  const outerHeight = positive(input.outerHeight, 1)
  const naturalWidth = positive(input.naturalWidth, outerWidth)
  const naturalHeight = positive(input.naturalHeight, outerHeight)
  const sizing = String(input.sizingMode ?? 'Auto')
  const horizontal = input.horizontalAlignment ?? (sizing === 'OverflowCrop' ? 'Center' : sizing === 'FillContainer' ? 'Stretch' : 'Stretch')
  const vertical = input.verticalAlignment ?? (sizing === 'OverflowCrop' ? 'Center' : sizing === 'FillContainer' ? 'Stretch' : 'Stretch')
  const boxWidth = input.controlType === 'BorderedImage' && input.imageWidth != null ? positive(input.imageWidth, outerWidth) : outerWidth
  const boxHeight = input.controlType === 'BorderedImage' && input.imageHeight != null ? positive(input.imageHeight, outerHeight) : outerHeight
  const stretch = String(input.stretch ?? 'Fill')
  let width = naturalWidth
  let height = naturalHeight
  let objectFit: CSSProperties['objectFit'] = 'none'
  if (stretch === 'Fill') {
    width = boxWidth; height = boxHeight; objectFit = 'fill'
  } else if (stretch === 'Uniform' || stretch === 'UniformToFill') {
    const sx = boxWidth / naturalWidth; const sy = boxHeight / naturalHeight
    const scale = stretch === 'UniformToFill' ? Math.max(sx, sy) : Math.min(sx, sy)
    width = naturalWidth * scale; height = naturalHeight * scale
    objectFit = stretch === 'UniformToFill' ? 'cover' : 'contain'
  }
  const left = offset(horizontal, outerWidth, width)
  const top = offset(vertical, outerHeight, height)
  return {
    viewportStyle: {
      position: 'relative', width: '100%', height: '100%',
      overflow: input.clipToBounds || sizing === 'OverflowCrop' ? 'hidden' : 'visible',
      borderRadius: input.cornerRadius && input.cornerRadius > 0 ? input.cornerRadius : undefined,
    },
    imageStyle: { position: 'absolute', width, height, left, top, objectFit: 'fill', maxWidth: 'none', maxHeight: 'none' },
    imageLayoutWidth: width,
    imageLayoutHeight: height,
    imageOffsetX: left,
    imageOffsetY: top,
    objectFit,
  }
}
