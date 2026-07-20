import { useEffect, useState } from 'react'
import type { CSSProperties } from 'react'

type DecodedImage = { source: string; generation: number; naturalWidth: number; naturalHeight: number }

export function DynamicImage({ source, generation, style, onDecoded }: { source: string | null; generation: number; style: CSSProperties; onDecoded?: (width: number, height: number) => void }) {
  const [current, setCurrent] = useState<DecodedImage | null>(null)
  useEffect(() => {
    let cancelled = false
    if (source === null) { setCurrent(null); return }
    const image = new Image()
    image.src = source
    const ready = typeof image.decode === 'function' ? image.decode() : new Promise<void>((resolve, reject) => { image.onload = () => resolve(); image.onerror = () => reject(new Error('ImageDecodeFailed')) })
    void ready.then(() => {
      if (cancelled) return
      const decoded = { source, generation, naturalWidth: image.naturalWidth, naturalHeight: image.naturalHeight }
      setCurrent(decoded)
      onDecoded?.(decoded.naturalWidth, decoded.naturalHeight)
    }).catch(() => console.warn('[Web Renderer] image decode failed.'))
    return () => { cancelled = true }
  }, [source, generation, onDecoded])
  return current?.generation === generation ? <img data-image-element src={current.source} style={style} draggable={false} /> : null
}
