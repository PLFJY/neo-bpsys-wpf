import type { CSSProperties } from 'react'
import type { WebRuntimeAsset } from '../../protocol/runtime'
import type { ImageConfig } from '../controlTypes'

export type ImageLayoutInput = {
  controlType: ImageConfig['ControlType']
  outerWidth: number
  outerHeight: number
  imageWidth?: number | null
  imageHeight?: number | null
  naturalWidth: number
  naturalHeight: number
  stretch?: string | number
  sizingMode?: string | number
  horizontalAlignment?: string | number
  verticalAlignment?: string | number
  clipToBounds?: boolean
  cornerRadius?: number
}

export type ImageLayoutResult = {
  viewportStyle: CSSProperties
  imageStyle: CSSProperties
  imageLayoutWidth: number
  imageLayoutHeight: number
  imageOffsetX: number
  imageOffsetY: number
  objectFit: CSSProperties['objectFit']
}

export type ImageSourceState = { source: string | null; asset?: WebRuntimeAsset }
