import type { CSSProperties, ReactNode } from 'react'
import { useLayoutEffect, useRef, useState } from 'react'
import { color } from '../colors'
import { horizontal, textAlign, vertical, wrapping } from '../alignment'
import { finite, type GameProgressConfig } from '../controlTypes'
import { fontFamily, fontWeight } from '../fonts'
import type { RuntimeState } from '../../protocol/runtime'

export const displayModeNames = [
  'Inline',
  'TwoLine',
  'VerticalHalfOnly',
  'VerticalGameOnly',
  'VerticalGameAndHalf',
  'VerticalSeparatedGameAndHalf',
  'RibbonGameOnly',
  'HorizontalGameOnly',
  'HorizontalHalfOnly',
  'Vertical',
  'VerticalTwoLine',
] as const

export type GameProgressDisplayModeName = typeof displayModeNames[number]
export type VerticalLanguageModeName = 'Auto' | 'Upright' | 'RotateBlock' | 'StackCharacters'
export type LatinVerticalModeName = 'RotateBlock' | 'StackCharacters'
export type VerticalDirectionName = 'Auto' | 'FacingLeft' | 'FacingRight'

export const enumValue = <T extends string>(value: unknown, names: readonly T[], fallback: T): T => {
  if (typeof value === 'number') return names[value] ?? fallback
  if (typeof value === 'string' && (names as readonly string[]).includes(value)) return value as T
  return fallback
}

export function resolveDisplayMode(value: unknown): GameProgressDisplayModeName {
  return enumValue(value, displayModeNames, 'Inline')
}

export function resolveVerticalLanguageMode(config: GameProgressConfig, isCjkCulture: boolean): Exclude<VerticalLanguageModeName, 'Auto'> {
  const configured = enumValue(config.VerticalLanguageMode, ['Auto', 'Upright', 'RotateBlock', 'StackCharacters'] as const, 'Auto')
  if (configured !== 'Auto') return configured
  if (isCjkCulture) return 'Upright'
  return enumValue(config.LatinVerticalMode, ['RotateBlock', 'StackCharacters'] as const, 'RotateBlock')
}

export function resolveVerticalDirection(value: unknown): Exclude<VerticalDirectionName, 'Auto'> {
  const direction = enumValue(value, ['Auto', 'FacingLeft', 'FacingRight'] as const, 'Auto')
  return direction === 'FacingRight' ? 'FacingRight' : 'FacingLeft'
}

export function rotationDegrees(direction: Exclude<VerticalDirectionName, 'Auto'>): -90 | 90 {
  return direction === 'FacingRight' ? 90 : -90
}

export function segmentGraphemes(value: string, culture?: string): string[] {
  if (!value) return []
  try {
    if (typeof Intl.Segmenter === 'function') {
      const segmenter = new Intl.Segmenter(culture, { granularity: 'grapheme' })
      return [...segmenter.segment(value)].map(item => item.segment)
    }
  } catch {
    // Invalid/unsupported culture falls through to the safe segmentation fallback.
  }
  return Array.from(value)
}

export function rotatedLayoutSize(width: number, height: number): { width: number; height: number } {
  return { width: height, height: width }
}

export function GameProgressRoot({ config, runtime, verticalLayout, children }: { config: GameProgressConfig; runtime: RuntimeState; verticalLayout: boolean; children: ReactNode }) {
  const boundColor = config.ColorBindingPath ? runtime.values[config.ColorBindingPath] : undefined
  const rootStyle: CSSProperties = {
    position: 'absolute',
    inset: 0,
    boxSizing: 'border-box',
    overflow: 'visible',
    color: color(boundColor ?? config.Color, '#fff'),
    background: color(config.BackgroundColor),
    fontFamily: fontFamily(config.FontFamily) ?? 'system-ui, "Segoe UI", sans-serif',
    fontWeight: fontWeight(config.FontWeight),
    fontSize: typeof config.FontSize === 'number' && config.FontSize > 0 ? config.FontSize : undefined,
    lineHeight: 'normal',
    padding: `${finite(config.PaddingTop)}px ${finite(config.PaddingRight)}px ${finite(config.PaddingBottom)}px ${finite(config.PaddingLeft)}px`,
  }
  const contentSlotStyle: CSSProperties = {
    display: 'flex',
    alignItems: verticalLayout ? 'center' : vertical(config.VerticalAlignment),
    justifyContent: verticalLayout ? 'center' : horizontal(config.HorizontalAlignment),
    textAlign: verticalLayout ? undefined : textAlign(config.TextAlignment),
    whiteSpace: verticalLayout ? undefined : wrapping(config.TextWrapping),
    width: '100%',
    height: '100%',
    minWidth: 0,
    minHeight: 0,
  }
  return <div data-game-progress-root data-behavior-content style={rootStyle}><div data-game-progress-content-slot style={contentSlotStyle}>{children}</div></div>
}

export function GameProgressHorizontalLayout({ children }: { children: ReactNode }) {
  return <span data-game-progress-horizontal-layout style={{ display: 'contents' }}>{children}</span>
}

export function GameProgressVerticalLayout({ children }: { children: ReactNode }) {
  return <div data-game-progress-vertical-layout style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: '100%', height: '100%', minWidth: 0, minHeight: 0 }}>{children}</div>
}

function CharacterColumn({ value, culture, spacing, kind }: { value: string; culture?: string; spacing: number; kind: 'Upright' | 'StackCharacters' }) {
  const characters = segmentGraphemes(value, culture)
  return <div data-game-progress-vertical={kind} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', width: 'max-content', height: 'max-content' }}>
    {characters.map((character, index) => <span key={`${index}:${character}`} style={{ display: 'block', whiteSpace: 'pre', marginBottom: index < characters.length - 1 ? spacing : 0 }}>{character}</span>)}
  </div>
}

export function UprightVerticalText({ value, culture, spacing }: { value: string; culture?: string; spacing: number }) {
  return <CharacterColumn value={value} culture={culture} spacing={spacing} kind="Upright" />
}

export function StackedVerticalText({ value, culture, spacing }: { value: string; culture?: string; spacing: number }) {
  return <CharacterColumn value={value} culture={culture} spacing={spacing} kind="StackCharacters" />
}

type LayoutSize = { width: number; height: number }

export type RotatedLayoutBoxProps = {
  direction: 'FacingLeft' | 'FacingRight'
  children: ReactNode
}

export function RotatedLayoutBox({ direction, children }: RotatedLayoutBoxProps) {
  const measurementRef = useRef<HTMLDivElement>(null)
  const [size, setSize] = useState<LayoutSize>({ width: 0, height: 0 })

  useLayoutEffect(() => {
    const measurement = measurementRef.current
    if (!measurement) return

    let disposed = false
    const measure = () => {
      if (disposed) return
      const next = { width: measurement.offsetWidth, height: measurement.offsetHeight }
      setSize(previous => previous.width === next.width && previous.height === next.height ? previous : next)
    }

    const observer = typeof ResizeObserver === 'function' ? new ResizeObserver(measure) : undefined
    observer?.observe(measurement)
    measure()

    const resize = observer ? undefined : measure
    if (resize) window.addEventListener('resize', resize)

    const fonts = document.fonts
    void fonts?.ready.then(measure)
    fonts?.addEventListener('loadingdone', measure)

    return () => {
      disposed = true
      observer?.disconnect()
      if (resize) window.removeEventListener('resize', resize)
      fonts?.removeEventListener('loadingdone', measure)
    }
  }, [children, direction])

  const measured = size.width > 0 && size.height > 0
  const outer = measured ? rotatedLayoutSize(size.width, size.height) : { width: 'max-content', height: 'max-content' }
  const degrees = rotationDegrees(direction)
  const innerStyle: CSSProperties = {
    position: 'absolute',
    left: '50%',
    top: '50%',
    width: measured ? size.width : 'max-content',
    height: measured ? size.height : 'max-content',
    display: 'block',
    whiteSpace: 'nowrap',
    transform: `translate(-50%, -50%) rotate(${degrees}deg)`,
    transformOrigin: 'center center',
  }
  return <div data-game-progress-rotated-layout data-game-progress-rotation={degrees} style={{ position: 'relative', display: 'grid', placeItems: 'center', flex: '0 0 auto', width: outer.width, height: outer.height }}>
    <div ref={measurementRef} aria-hidden="true" data-game-progress-measurement style={{ position: 'absolute', left: 0, top: 0, width: 'max-content', height: 'max-content', visibility: 'hidden', pointerEvents: 'none', whiteSpace: 'nowrap' }}>{children}</div>
    <div data-game-progress-rotated-content style={innerStyle}>{children}</div>
  </div>
}
