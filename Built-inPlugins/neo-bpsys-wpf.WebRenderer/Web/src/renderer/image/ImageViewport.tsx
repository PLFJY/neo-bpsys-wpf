import { useCallback, useEffect, useState } from 'react'
import type { WebRuntimeAsset } from '../../protocol/runtime'
import type { ImageConfig } from '../controlTypes'
import { calculateImageLayout } from './ImageLayoutEngine'
import { DynamicImage } from './DynamicImage'

export function ImageViewport({ config, source, asset, generation }: { config: ImageConfig; source: string | null; asset?: WebRuntimeAsset; generation: number }) {
  const [decodedSize, setDecodedSize] = useState<{ width: number; height: number } | null>(null)
  useEffect(() => setDecodedSize(null), [source, generation])
  const onDecoded = useCallback((width: number, height: number) => setDecodedSize({ width, height }), [])
  const outerWidth = typeof config.Width === 'number' ? config.Width : 1
  const outerHeight = typeof config.Height === 'number' ? config.Height : 1
  const layout = calculateImageLayout({
    controlType: config.ControlType,
    outerWidth,
    outerHeight,
    imageWidth: config.ImageWidth,
    imageHeight: config.ImageHeight,
    naturalWidth: asset?.NaturalWidthDip ?? asset?.Width ?? decodedSize?.width ?? outerWidth,
    naturalHeight: asset?.NaturalHeightDip ?? asset?.Height ?? decodedSize?.height ?? outerHeight,
    stretch: config.Stretch,
    sizingMode: config.SizingMode,
    horizontalAlignment: config.HorizontalAlignment,
    verticalAlignment: config.VerticalAlignment,
    clipToBounds: config.ClipToBounds,
    cornerRadius: config.CornerRadius,
  })
  return <div data-behavior-content data-content-viewport style={layout.viewportStyle}><DynamicImage source={source} generation={generation} style={layout.imageStyle} onDecoded={onDecoded} /></div>
}
